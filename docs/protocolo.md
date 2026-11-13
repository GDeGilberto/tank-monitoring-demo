# Protocolo propio de tramas

Este documento describe, con palabras propias, el protocolo que usa el sistema entre el lector de tanques y el software. Fue definido por este proyecto. No reproduce ningún manual ni protocolo de un fabricante: un hardware real que hable otro protocolo se conecta con un **adaptador** que lo traduzca a este (fuera del alcance del demo).

## Abstracción

El software habla con un `ITankReader`: `SendCommandAsync(comando, tiempo de espera)` devuelve `Ok` con la trama completa, `Timeout` o `PortError`. Hay dos implementaciones:

- `SerialPortManager`: puerto serial real (semáforo, tiempo de espera por inactividad y cierre del puerto en cada lectura).
- `SimulatedTankReader`: simulador determinista, activado con `Reader:Mode=Simulated`.

## Trama

```
SOH  encabezado(6)  fecha(10)  bloques...  &&  checksum(4)  ETX
```

- `SOH` = 0x01 y `ETX` = 0x03.
- Encabezado: código de 3 letras + `0` + tanque de 2 dígitos (`00` = todos). Se devuelve igual que llegó.
- Fecha `yyMMddHHmm`, cultura invariante.
- Números reales: 8 caracteres hexadecimales, IEEE 754, big-endian.
- Checksum: complemento a 2 (módulo 65536) de la suma de todos los caracteres desde `SOH` hasta `&&` inclusive; 4 hex en mayúsculas. **Se valida siempre antes de leer ningún campo** (`TankFrame.Unwrap`); no hay bandera para omitirlo.

## Comandos

| Comando | Respuesta |
|---|---|
| `INV000` | Inventario. Por tanque: `TT` (decimal) + tipo (`A`, `B` o `C`) + estado `ssss` (4 hex) + `NN` (2 hex, cantidad de campos) + 7 floats: volumen, volumen compensado, ullage, altura, agua, temperatura, volumen de agua |
| `ENT000` | Entregas (descargas). Por tanque: `TT` + tipo + `dd` (decimal, cantidad) y por entrega: fecha inicio, fecha fin, `NN` y 10 floats (inicio: volumen, compensado, agua, temperatura; fin: lo mismo; altura inicio, altura fin) |
| cualquier otro | `SOH ERR0 FEE6 ETX` (sin `&&`) |

Vector de prueba: `ERR0` -> checksum `FEE6`.

## Bits de estado (`ssss`)

| Bit | Significado |
|---|---|
| `0x0001` | Entrega en curso |
| `0x0004` | Altura inválida (tanque casi vacío) |

## Unidades

Volúmenes en litros, alturas y agua en milímetros, temperatura en °C. La base de datos solo guarda volumen y temperatura del inventario; altura y agua viajan en la trama y se ven en las pruebas.

## Errores de lectura

`TankFrameException` con un `TankFrameError`: vacía, sin SOH, sin ETX, sin terminador, checksum de largo incorrecto, checksum que no coincide, `ERR0`, truncada, fecha inválida, float inválido (NaN o infinito) o campo inválido. El job registra una advertencia y no guarda nada.
