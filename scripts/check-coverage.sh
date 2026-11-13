#!/usr/bin/env bash
# Pruebas con cobertura y umbrales: protocolo/parsers >= 90 %, jobs y repositorios >= 80 %.
# Requiere dotnet y ${PYTHON:-python3}.
set -euo pipefail
PYTHON=${PYTHON:-$(command -v python3 || command -v python)}
cd "$(dirname "$0")/.."
rm -rf TestResults
dotnet test Test/Test.csproj --filter "Category!=Hardware" --collect:"XPlat Code Coverage" --results-directory TestResults
REPORT=$(find TestResults -name coverage.cobertura.xml | head -n 1)
${PYTHON:-python3} - "$REPORT" <<'PY'
import sys, re
import xml.etree.ElementTree as ET
root = ET.parse(sys.argv[1]).getroot()
groups = {
    "protocolo y parsers (>= 90 %)": (90, re.compile(r"^Application\.(Protocol\.|UseCases\.Parse(TankInventoryReport|ceDeliveryReport)|UseCases\.ParceDeliveryReport)")),
    "jobs (>= 80 %)": (80, re.compile(r"^Infrastructure\.Jobs\.(InventarioJob|DescargasJobs)")),
    "repositorios (>= 80 %)": (80, re.compile(r"^Infrastructure\.Repositories\.(DescargasRepository|InventarioRepository)")),
}
failed = False
for title, (minimum, rx) in groups.items():
    covered = total = 0
    for cls in root.iter("class"):
        if rx.search(cls.get("name", "")):
            for line in cls.iter("line"):
                total += 1
                covered += 1 if int(line.get("hits", "0")) > 0 else 0
    pct = 100.0 * covered / total if total else 0.0
    ok = pct >= minimum
    failed |= not ok
    print(f"{'OK ' if ok else 'FALLA'} {title}: {pct:.1f} % ({covered}/{total})")
sys.exit(1 if failed else 0)
PY
