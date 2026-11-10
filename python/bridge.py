"""JSON entry point for Pika weather and legacy solar conditions."""
import json
import sys


def weather(source, start, end):
    import numpy as np
    import pandas as pd
    from pvlib.iotools import read_epw
    from pvlib.solarposition import get_solarposition

    frame, site = read_epw(source, coerce_year=2001)
    expected = pd.date_range('2001-01-01', periods=8760, freq='h', tz=frame.index.tz)
    if not frame.index.equals(expected):
        raise ValueError('EPW must contain an ordered, complete non-leap year of hourly records.')
    def boundary(text, default):
        return pd.Timestamp('2001/' + text, tz=frame.index.tz) if text else default
    begin = boundary(start, expected[0])
    stop = boundary(end, expected[-1] + pd.Timedelta(hours=1))
    if end and stop <= begin:
        raise ValueError('Period End must follow Period Start within the EPW year.')
    frame = frame[(frame.index >= begin) & (frame.index < stop)]
    if frame.empty:
        raise ValueError('The selected period contains no hourly intervals.')
    fields = {'dni': ('Dni', 9999), 'dhi': ('Dhi', 9999), 'ghi': ('Ghi', 9999),
              'temp_air': ('AirTempC', 99.9), 'wind_speed': ('WindMS', 999)}
    values = {}
    for column, (key, missing) in fields.items():
        series = frame[column]
        if not np.isfinite(series).all() or (series == missing).any() or (
            column != 'temp_air' and (series < 0).any()
        ):
            raise ValueError('EPW has missing or invalid ' + column + ' in the selected period.')
        values[key] = series.tolist()
    sun = get_solarposition(frame.index + pd.Timedelta(minutes=30),
                            site['latitude'], site['longitude'], altitude=site['altitude'])
    return dict(values, Timestamps=[t.isoformat() for t in frame.index],
                Elevation=sun['apparent_elevation'].tolist(), Azimuth=sun['azimuth'].tolist(),
                Latitude=site['latitude'], Longitude=site['longitude'],
                Altitude=site['altitude'], UtcOffset=site['TZ'])


def dispatch(operation, inputs):
    if operation == 'weather':
        return [weather(**inputs)]
    if operation == 'solar_conditions':
        from solar_conditions import prepare
        return [prepare(**inputs)]
    raise ValueError('Unsupported operation: ' + str(operation))


if __name__ == '__main__':
    try:
        request = json.load(sys.stdin)
        print(json.dumps({'outputs': dispatch(request['operation'], request['inputs'])}, allow_nan=False))
    except Exception as error:
        print(json.dumps({'error': str(error)}))
        sys.exit(1)
