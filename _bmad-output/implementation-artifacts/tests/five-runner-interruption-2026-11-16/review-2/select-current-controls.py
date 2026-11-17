"""Bind marker controls to actual exclusive command births and passing XML rows."""
import json
import re
import xml.etree.ElementTree as ET
from pathlib import Path

private = Path(__file__).resolve().parent
active = private / 'current-layout'
evidence = active / 'early-controls-package-final'

def command(label):
    return json.loads((active / 'queue' / (label + '.result')).read_text())

def methods(label):
    result = {}
    for test in ET.parse(active / (label + '.xml')).findall('.//test'):
        if '.FailedConsumerTerminationRetainsBothRemovalGatesUntilExactExit(' not in test.attrib['name']:
            continue
        gate = re.search(r'gate: "([^"]+)"', test.attrib['name'].replace('\\"', '"')).group(1)
        assert gate not in result and test.attrib['result'] == 'Pass'
        result[gate] = test.attrib['name']
    assert set(result) == {'application', 'control-plane', 'stale-quiescence'}
    return result

current = command('62-final-controls')
following = command('63-consumer-unit')
historical = command('45-final-controls')
historical_following = command('46-consumer-unit')
assert current['ExitCode'] == following['ExitCode'] == 0
assert all(item['DescendantsReaped'] for item in [current, following, historical, historical_following])
lower, upper = int(current['Supervisor']['Start']), int(following['Supervisor']['Start'])
old_lower, old_upper = int(historical['Supervisor']['Start']), int(historical_following['Supervisor']['Start'])
assert old_lower < old_upper < lower < upper
current_methods, old_methods = methods('62-final-controls'), methods('45-final-controls')
selected, old = [], []
for path in sorted(evidence.glob('consumer-gate-*-custody.json')):
    custody = json.loads(path.read_text())
    outcome_path = path.with_name(path.name.replace('-custody.json', '.json'))
    outcome = json.loads(outcome_path.read_text())
    assert all(custody[key] == outcome[key] for key in ['Gate', 'ConsumerPid', 'ConsumerStart'])
    assert custody['Id'] in path.name and custody['Marker'] and custody['BackingId'] and custody['NetworkId']
    ticks = [custody['ConsumerStart'], custody['ControllerStart']]
    entry = {'Gate': custody['Gate'], 'Outcome': 'current-layout/early-controls-package-final/' + outcome_path.name + '.gz',
             'Custody': 'current-layout/early-controls-package-final/' + path.name + '.gz',
             'ConsumerPid': custody['ConsumerPid'], 'ConsumerStart': custody['ConsumerStart'],
             'ControllerPid': custody['ControllerPid'], 'ControllerStart': custody['ControllerStart'], 'SessionId': custody['Id']}
    if all(lower <= tick < upper for tick in ticks):
        entry['XmlMethod'] = current_methods[custody['Gate']]
        selected.append(entry)
    else:
        assert all(old_lower <= tick < old_upper for tick in ticks)
        entry.update({'XmlMethod': old_methods[custody['Gate']], 'CurrentAcceptance': False})
        old.append(entry)
assert len(selected) == len(old) == 3
assert {entry['Gate'] for entry in selected} == {entry['Gate'] for entry in old} == set(current_methods)
result = {'Derivation': 'Both consumer/controller kernel Start ticks fall within the actual exclusive command62-to63 supervisor birth interval; matching outcome/custody and passing XML rows are required',
          'Command': '62-final-controls', 'NextCommand': '63-consumer-unit', 'Xml': 'current-layout/62-final-controls.xml.gz',
          'LowerInclusiveStart': lower, 'UpperExclusiveStart': upper, 'SelectedGuards': selected,
          'HistoricalCommand': '45-final-controls', 'HistoricalNextCommand': '46-consumer-unit',
          'HistoricalLowerInclusiveStart': old_lower, 'HistoricalUpperExclusiveStart': old_upper,
          'HistoricalXml': 'current-layout/45-final-controls.xml.gz', 'ExcludedHistoricalGuards': old,
          'FilesystemMtimeUsedAsAuthority': False, 'OriginalRefusal': 'packet-marker-selection-refusal.json.gz'}
(private / 'current-controls-selection.json').write_text(json.dumps(result, indent=2) + '\n')
print(json.dumps({'CurrentGuards': len(selected), 'RetainedHistoricalGuards': len(old), 'StartInterval': [lower, upper]}))
