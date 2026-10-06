"""Stub local de SharePoint para la demo de Etapa 20 (Ejemplo 2).

Simula SOLO el lado servidor con la misma forma REST de SharePoint:
- POST /token            -> OAuth2 client_credentials (access_token falso)
- GET  /sites/demo/_api/web/lists -> bibliotecas en forma SharePoint
- GET  /sites/demo/_api/web/lists(guid)/items -> documentos de una biblioteca

Todo lo demás (OAuth2, _api, cabecera Authorization, auditoría) lo ejecuta
el conector real contra este stub por HTTP real. Etiquetar como SIMULADO.
"""
import json
from http.server import BaseHTTPRequestHandler, HTTPServer
from urllib.parse import urlparse

LISTAS = {
    "d1": {
        "Title": "Documentos Compartidos",
        "ItemCount": 2,
        "items": [
            {"Id": 1, "Title": "Manual de inventario.pdf",
             "Url": "/sites/demo/Documentos/Manual de inventario.pdf",
             "Modified": "2026-09-20T00:00:00Z"},
            {"Id": 2, "Title": "Politica de stock.xlsx",
             "Url": "/sites/demo/Documentos/Politica de stock.xlsx",
             "Modified": "2026-09-27T00:00:00Z"},
        ],
    },
    "d2": {
        "Title": "Reportes",
        "ItemCount": 1,
        "items": [
            {"Id": 1, "Title": "Reporte de insumos.pdf",
             "Url": "/sites/demo/Reportes/Reporte de insumos.pdf",
             "Modified": "2026-10-06T00:00:00Z"},
        ],
    },
}


class Manejador(BaseHTTPRequestHandler):
    server_version = "StubSharePoint/1.0"

    def _json(self, codigo, objeto):
        cuerpo = json.dumps(objeto).encode("utf-8")
        self.send_response(codigo)
        self.send_header("Content-Type", "application/json;odata=verbose")
        self.send_header("Content-Length", str(len(cuerpo)))
        self.end_headers()
        self.wfile.write(cuerpo)

    def log_message(self, *args):
        pass

    def do_POST(self):
        ruta = urlparse(self.path).path
        largo = int(self.headers.get("Content-Length", 0))
        self.rfile.read(largo)
        if ruta == "/token":
            self._json(200, {"access_token": "stub-token-demo",
                             "token_type": "Bearer", "expires_in": 3600})
        else:
            self._json(404, {"error": "ruta no simulada: " + ruta})

    def do_GET(self):
        ruta = urlparse(self.path).path
        auth = self.headers.get("Authorization", "")
        if auth != "Bearer stub-token-demo":
            self._json(401, {"error": "falta Bearer stub-token-demo"})
            return
        # ---- Microsoft Graph (simulado, misma forma) ----
        if ruta in ("/v1.0/users", "/v1.0/users/"):
            self._json(200, {"value": [
                {"id": "u1", "displayName": "Maria Johnson",
                 "mail": "emailmaria@demo.local", "jobTitle": "Gerencia"},
                {"id": "u2", "displayName": "Brian Halligan",
                 "mail": "bh@demo.local", "jobTitle": "Operaciones"},
            ]})
            return
        if ruta in ("/v1.0/me/messages", "/v1.0/me/messages/"):
            self._json(200, {"value": [
                {"id": "m1", "subject": "Reporte de insumos",
                 "from": {"emailAddress": {"address": "emailmaria@demo.local"}},
                 "receivedDateTime": "2026-10-06T00:00:00Z"},
            ]})
            return
        # ---- SharePoint REST (simulado, misma forma) ----
        if ruta.endswith("/_api/web/lists"):
            self._json(200, {"d": {"results": [
                {"Id": k, "Title": v["Title"], "ItemCount": v["ItemCount"]}
                for k, v in LISTAS.items()]}})
        elif "/_api/web/lists(" in ruta and ruta.endswith(")/items"):
            guid = ruta.split("lists(")[1].split(")")[0].strip("'")
            biblio = LISTAS.get(guid)
            if biblio is None:
                self._json(404, {"error": "biblioteca inexistente"})
            else:
                self._json(200, {"d": {"results": biblio["items"]}})
        else:
            self._json(404, {"error": "ruta no simulada: " + ruta})


if __name__ == "__main__":
    servidor = HTTPServer(("0.0.0.0", 8899), Manejador)
    print("Stub SharePoint en http://0.0.0.0:8899 (Ctrl+C para detener)")
    servidor.serve_forever()
