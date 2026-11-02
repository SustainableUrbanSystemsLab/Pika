"""Narrow JSON bridge for the current Solar Conditions component only."""
import json,sys
from solar_conditions import prepare

def dispatch(operation,inputs):
    if operation != 'solar_conditions':
        raise ValueError('Unsupported operation: '+str(operation))
    return [prepare(**inputs)]

if __name__=='__main__':
    try:
        request=json.load(sys.stdin)
        print(json.dumps({'outputs':dispatch(request['operation'],request['inputs'])},allow_nan=False))
    except Exception as error:
        print(json.dumps({'error':str(error)}));sys.exit(1)
