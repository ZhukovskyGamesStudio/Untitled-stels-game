#!/usr/bin/env bash
# Tools/unity_test.sh <TestName|Class> [more…] — runs EditMode tests in the OPEN editor through its MCP bridge and waits.
ROOT="$(cd "$(dirname "$0")/.." && pwd)"
names=$(printf '"%s",' "$@"); names="[${names%,}]"
key="test_names"; [ -n "${GROUP:-}" ] && key="group_names"
job=$(python "$ROOT/Tools/unity_mcp.py" run_tests "{\"mode\":\"EditMode\",\"$key\":$names,\"include_failed_tests\":true}" | python -c "import sys,json; print(json.load(sys.stdin)['result']['data']['job_id'])")
for i in $(seq 1 ${WAIT:-240}); do
  out=$(python "$ROOT/Tools/unity_mcp.py" get_test_job "{\"job_id\":\"$job\",\"include_failed_tests\":true}" 20000 2>/dev/null)
  case "$out" in *'"status":"running"'*) sleep 2 ;; *) break ;; esac
done
echo "$out" | python -c "
import sys, json
d = json.load(sys.stdin)['result']['data']
r = d.get('result') or {}
s = r.get('summary') or {}
print('tests:', d.get('status'), 'total=%s passed=%s failed=%s skipped=%s' % (s.get('total'), s.get('passed'), s.get('failed'), s.get('skipped')), 'in %.1fs' % (s.get('durationSeconds') or 0))
for t in (r.get('failed_tests') or r.get('results') or []):
    if t.get('state', 'Failed') != 'Passed': print('FAILED', t.get('fullName'), '-', (t.get('message') or '')[:1500])
"
