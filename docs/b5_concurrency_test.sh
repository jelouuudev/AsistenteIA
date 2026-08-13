#!/usr/bin/env bash
# B5 - Pruebas de concurrencia contra el stack Docker (Opción A)
# Mide endpoints rápidos (health, permisos) a N=5/10/25/50 y chat RAG (Ollama CPU) a N=1/3.
set -u
BASE="http://localhost:5298/api"
OUT="docs/ETAPA15_reporte_concurrencia.json"
TOKEN=$(curl -s -m 15 -X POST "$BASE/auth/login" -H "Content-Type: application/json" -d '{"Usuario":"admin","Contrasena":"Admin123*"}' | sed -E 's/.*"token":"([^"]+)".*/\1/')
echo "token len=${#TOKEN}"
AUTH="Authorization: Bearer $TOKEN"

measure() {
  local endpoint="$1"; local n="$2"; local auth="$3"; local label="$4"
  local start=$(date +%s%3N)
  local ok=0 err=0
  local pids=()
  for i in $(seq 1 $n); do
    (
      local code
      if [ -n "$auth" ]; then
        code=$(curl -s -o /dev/null -w "%{http_code}" -m 30 "$BASE$endpoint" -H "$auth")
      else
        code=$(curl -s -o /dev/null -w "%{http_code}" -m 30 "$BASE$endpoint")
      fi
      [ "$code" = "200" ] && exit 0 || exit 1
    ) &
    pids+=($!)
  done
  for p in "${pids[@]}"; do
    wait $p && ok=$((ok+1)) || err=$((err+1))
  done
  local end=$(date +%s%3N)
  local total=$(( (end-start)/1000 ))
  printf '{"label":"%s","endpoint":"%s","N":%s,"exitosas":%s,"errores":%s,"tiempo_total_s":%s}\n' "$label" "$endpoint" "$n" "$ok" "$err" "$total"
}

echo "=== Endpoints rapidos (sin Ollama) ==="
measure "/health" 5 "" "health_N5" >> "$OUT.tmp"
measure "/health" 10 "" "health_N10" >> "$OUT.tmp"
measure "/health" 25 "" "health_N25" >> "$OUT.tmp"
measure "/health" 50 "" "health_N50" >> "$OUT.tmp"
measure "/seguridad/permisos" 5 "$AUTH" "permisos_N5" >> "$OUT.tmp"
measure "/seguridad/permisos" 10 "$AUTH" "permisos_N10" >> "$OUT.tmp"
measure "/seguridad/permisos" 25 "$AUTH" "permisos_N25" >> "$OUT.tmp"
measure "/seguridad/permisos" 50 "$AUTH" "permisos_N50" >> "$OUT.tmp"

echo "=== Chat RAG (Ollama CPU, lento) N=1 y N=3 ==="
# N=1
t0=$(date +%s%3N)
curl -s -m 300 -X POST "$BASE/chat/enviar" -H "$AUTH" -H "Content-Type: application/json" --data-binary @docs/b4_chat_request.json > /tmp/b5_chat1.json
t1=$(date +%s%3N)
tr1=$(( (t1-t0)/1000 ))
rt1=$(grep -oE '"tiempoRespuestaMs":[0-9]+' /tmp/b5_chat1.json | head -1 | sed 's/[^0-9]//g')
ctx1=$(grep -oE '"tiempoConstruccionContextoMs":[0-9]+' /tmp/b5_chat1.json | head -1 | sed 's/[^0-9]//g')
ex1=$(grep -oE '"exitoso":(true|false)' /tmp/b5_chat1.json | head -1 | sed 's/.*://')
printf '{"label":"chat_rag_N1","N":1,"exitoso":"%s","tiempo_total_s":%s,"TiempoRespuestaMs":%s,"TiempoConstruccionContextoMs":%s}\n' "$ex1" "$tr1" "$rt1" "$ctx1" >> "$OUT.tmp"

# N=3 (paralelo, cada uno ~2min CPU)
t0=$(date +%s%3N)
for i in 1 2 3; do
  curl -s -m 300 -X POST "$BASE/chat/enviar" -H "$AUTH" -H "Content-Type: application/json" --data-binary @docs/b4_chat_request.json > /tmp/b5_chat3_$i.json &
done
wait
t1=$(date +%s%3N)
tr3=$(( (t1-t0)/1000 ))
ok3=0
for i in 1 2 3; do grep -q '"exitoso":true' /tmp/b5_chat3_$i.json && ok3=$((ok3+1)); done
rt3=$(grep -oE '"tiempoRespuestaMs":[0-9]+' /tmp/b5_chat3_1.json | head -1 | sed 's/[^0-9]//g')
ctx3=$(grep -oE '"tiempoConstruccionContextoMs":[0-9]+' /tmp/b5_chat3_1.json | head -1 | sed 's/[^0-9]//g')
printf '{"label":"chat_rag_N3","N":3,"exitosas":%s,"tiempo_total_s":%s,"TiempoRespuestaMs_ejemplo":%s,"TiempoConstruccionContextoMs_ejemplo":%s}\n' "$ok3" "$tr3" "$rt3" "$ctx3" >> "$OUT.tmp"

# Empaquetar JSON
echo "[" > "$OUT"
sed 's/$/,/' "$OUT.tmp" | sed '$ s/,$//' >> "$OUT"
echo "]" >> "$OUT"
rm -f "$OUT.tmp" /tmp/b5_chat*.json
echo "=== RESULTADO (docs/ETAPA15_reporte_concurrencia.json) ==="
cat "$OUT"
