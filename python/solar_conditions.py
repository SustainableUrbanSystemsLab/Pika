"""Normalize hourly EPW lists and generate time-resolved Radiance sky patches.
Uses installed Ladybug vector/solid-angle definitions, without copying its source.
"""
import os,sys,shutil,tempfile,subprocess
from pathlib import Path
import numpy as np
import pandas as pd
import pvlib

def prepare(latitude,longitude,timezone,altitude,dnr,dhr,ghr,hoys=None,north=0,albedo=.2,density=1,units='Wh/m2'):
    def scalar(v,name,lo,hi):
        v=float(v)
        if not np.isfinite(v) or not lo<=v<=hi:raise ValueError(name+' is out of range.')
        return v
    lat=scalar(latitude,'Latitude',-90,90);lon=scalar(longitude,'Longitude',-180,180);tz=scalar(timezone,'UTC offset',-12,14);alt=scalar(altitude,'Altitude',-500,10000)
    north=scalar(north,'North',-360,360);albedo=scalar(albedo,'Ground reflectance',0,1)
    arrays=[np.asarray(x,dtype=float) for x in (dnr,dhr,ghr)];n=len(arrays[0])
    if not 1<=n<=8760 or any(a.shape!=(n,) or not np.isfinite(a).all() or (a<0).any() or (a>=9999).any() for a in arrays):raise ValueError('DNR, DHR and GHR must be aligned finite nonnegative hourly lists (max 8760), without EPW missing-data flags.')
    if units not in ('Wh/m2','W/m2'):raise ValueError('Choose Wh/m2 per hourly interval or W/m2. No automatic guessing for unlabelled values.')
    # Native hourly intervals only: Wh/m² / 1 h == mean W/m².
    if hoys is None or len(hoys)==0:
        if n!=8760:raise ValueError('Connect zero-based Weather HOYs for partial-year numeric lists.')
        hoys=list(range(n))
    h=np.asarray(hoys,dtype=float)
    if h.shape!=(n,) or not np.isfinite(h).all() or (h<0).any() or (h>8759).any() or not np.equal(h,np.floor(h)).all() or len(set(h))!=n or (np.diff(h)<=0).any():raise ValueError('Weather HOYs must be unique, increasing zero-based hourly interval indices 0..8759. Use the source-convention menu for one-based records.')
    if density not in (1,2):raise ValueError('Sky density must be 1 or 2.')
    # Installed Ladybug is required for matching Tregenza/Reinhart indexing.
    for folder in [Path.home()/'ladybug_tools/python/lib/python3.10/site-packages',Path.home()/'ladybug_tools/python/lib/python3.11/site-packages']:
        if folder.is_dir():sys.path.append(str(folder))
    try:from ladybug.viewsphere import view_sphere
    except ImportError:raise ValueError('Ladybug Tools Python libraries are required for the sky-patch definitions. No simplified fallback was used.')
    exe=os.environ.get('SOLAR_GENDAYMTX') or shutil.which('gendaymtx') or '/usr/local/radiance/bin/gendaymtx'
    if not Path(exe).is_file():raise ValueError('Radiance gendaymtx is missing. Install Radiance or set SOLAR_GENDAYMTX. No simplified fallback was used.')
    from datetime import timezone as fixed_timezone,timedelta
    zone=fixed_timezone(timedelta(hours=tz))
    dates=pd.date_range('2001-01-01',periods=8760,freq='h',tz=zone)[h.astype(int)]+pd.Timedelta(minutes=30)
    header='place PV_Solar_Conditions\nlatitude {}\nlongitude {}\ntime_zone {}\nsite_elevation {}\nweather_data_file_units 1\n'.format(lat,-lon,-15*tz,alt)
    rows=['{} {} {:.8f} {:.8f} {:.8f}'.format(d.month,d.day,d.hour+d.minute/60,arrays[0][i],arrays[1][i]) for i,d in enumerate(dates)]
    vectors=view_sphere.tregenza_dome_vectors if density==1 else view_sphere.reinhart_dome_vectors
    solid=np.asarray(view_sphere.tregenza_solid_angles if density==1 else view_sphere.reinhart_solid_angles)
    p=len(vectors)
    with tempfile.TemporaryDirectory(prefix='pv-sky-') as temp:
        wea=Path(temp)/'input.wea';wea.write_text(header+'\n'.join(rows)+'\n')
        def matrix(mode):
            cp=subprocess.run([exe,'-h','-of','-O1','-m',str(density),mode,str(wea)],capture_output=True,timeout=180)
            if cp.returncode:raise ValueError('gendaymtx failed: '+cp.stderr.decode(errors='replace')[:500])
            raw=np.frombuffer(cp.stdout,dtype=np.float32)
            if raw.size!=(p+1)*n*3:raise ValueError('Unexpected Radiance matrix dimensions.')
            rgb=raw.reshape(p+1,n,3)[1:]
            return np.maximum(0,np.sum(rgb*np.array([.265074126,.670114631,.064811243]),axis=2)*solid[:,None])
        direct=matrix('-d');diffuse=matrix('-s')
    pos=pvlib.solarposition.get_solarposition(dates,lat,lon,altitude=alt)
    return dict(Hoys=h.tolist(),Timestamps=[d.isoformat() for d in dates],StepHours=1.,North=north,Albedo=albedo,Vectors=[[v.x,v.y,v.z] for v in vectors],DirectW=direct.tolist(),DiffuseW=diffuse.tolist(),SunElevation=pos.apparent_elevation.tolist(),SunAzimuth=pos.azimuth.tolist(),Report='Radiance gendaymtx time-resolved Perez sky; {} patches; {} hourly intervals. Source: {}. Non-leap reference calendar 2001, local standard UTC{:+g}; HOY denotes interval start; sun positions evaluated at interval midpoint. Ground hemisphere follows Ladybug Incident Radiation approximation. No geometry or tree model is evaluated here.'.format(p,n,units,tz))
