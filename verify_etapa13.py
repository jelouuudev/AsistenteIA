import requests, json, time, jwt
from datetime import datetime, timedelta, timezone

API="http://localhost:5298"
SECRET="AsistenteIA_SuperSecretKey_2024_MustBeAtLeast32Chars!"
AUD="AsistenteIA_Users"
ISS="AsistenteIA"

# Generar JWT como admin (id 1)
token = jwt.encode({
    "nameid":"1","unique_name":"admin",
    "role":["Administrador"],
    "iss":ISS,"aud":AUD,
    "nbf": int((datetime.now(tz=timezone.utc)-timedelta(hours=24)).timestamp()),
    "exp": int((datetime.now(tz=timezone.utc)+timedelta(hours=8)).timestamp()),
    "jti": __import__("uuid").uuid4().hex
}, SECRET, algorithm="HS256")

h={"Authorization": f"Bearer {token}", "Content-Type":"application/json"}

print("== 1. Listar eventos ==")
r=requests.get(f"{API}/api/eventosempresariales", headers=h, timeout=20); print(r.status_code, r.text[:300])

print("\n== 2. Disparar evento DOC_PROCESADO (debe ejecutar ReporteClientes automaticamente) ==")
r=requests.post(f"{API}/api/eventomotor/disparar", headers=h, json={"CodigoEvento":"DOC_PROCESADO"}, timeout=30)
print(r.status_code, r.text[:400])

print("\n== 3. Esperar procesamiento (ProcesadorEventos cada 2s) ==")
time.sleep(8)
r=requests.get(f"{API}/api/eventosprocesados", headers=h, timeout=20)
print(r.status_code)
data=r.json()
for e in data[:3]:
    print(f"  Evento={e.get('nombreEvento')} Estado={e.get('estado')} Flujo={e.get('nombreWorkflow')} Tiempo={e.get('tiempoProcesamiento')} Resultado={str(e.get('resultado'))[:80]}")

print("\n== 4. Reglas y monitoreo ==")
r=requests.get(f"{API}/api/reglasevento", headers=h, timeout=20); print("Reglas:", r.status_code, [ (x.get('nombreEvento'), x.get('nombreWorkflow'), x.get('activa')) for x in r.json()])
r=requests.get(f"{API}/api/monitoreoeventos/panel", headers=h, timeout=20); print("Monitoreo:", r.status_code, r.json())
