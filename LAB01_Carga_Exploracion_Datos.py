import pandas as pd

# Cargar el archivo CSV
df = pd.read_csv("ventas.csv")

# Punto 6 - Actividad de comprobación
print("=" * 60)
print("PUNTO 6 - ACTIVIDAD DE COMPROBACIÓN")
print("=" * 60)

# 1. ¿Cuántos registros contiene el dataset?
print(f"1. Registros: {df.shape[0]}")

# 2. ¿Cuántas columnas contiene?
print(f"2. Columnas: {df.shape[1]}")

# 3. ¿Cuáles son los nombres de las columnas?
print(f"3. Nombres de columnas: {list(df.columns)}")

# 4. ¿Qué tipo de dato tiene la columna Cantidad?
print(f"4. Tipo de dato de Cantidad: {df['Cantidad'].dtype}")

# 5. ¿Qué tipo de dato tiene inicialmente la columna Fecha?
print(f"5. Tipo de dato de Fecha: {df['Fecha'].dtype}")

# 6. Según describe(), ¿cuál es el precio máximo registrado?
stats = df.describe()
print(f"6. Precio máximo registrado: {stats.loc['max', 'Precio']}")

# 7. Según describe(), ¿cuál es la cantidad máxima registrada en una venta?
print(f"7. Cantidad máxima registrada: {stats.loc['max', 'Cantidad']}")

print("=" * 60)
