# Créditos y atribución

Este mod es una **reimplementación desde cero** de las optimizaciones del mod original
**Performance Fish**, adaptada a RimWorld 1.6. **No es una copia del código original**: cada parche
fue reescrito y verificado contra el IL de Assembly-CSharp 1.6.9655. Pero el **catálogo de
optimizaciones, las ideas y la arquitectura de caches** provienen del original, y el mérito de
concebirlas es de su autor.

## Autor original

| | |
|---|---|
| **Autor** | bradson |
| **Mod original** | Performance Fish |
| **Repositorio** | [github.com/bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish) |
| **Biblioteca del autor** | [github.com/bbradson/Fishery](https://github.com/bbradson/Fishery) (Fishery, su librería de modding) |

## Autor de esta versión

| | |
|---|---|
| **Autor** | DGZ (serie Reforjed) |
| **Repositorio** | `https://github.com/DGZ/Reforjed` (enlazar aquí el repo real al publicar) |

## Qué se tomó del original y qué es propio

- **Tomado como referencia:** el *catálogo* de optimizaciones (qué métodos merecen cacheo), las
  estructuras de caches (by-reference, índices por def/grupo, grids de gas paralelos) y su
  filosofía de "medir antes de optimizar".
- **Escrito desde cero:** el código C# de esta versión, las reescrituras de IL vía Prepatcher,
  los enganches de runtime con Harmony, y la verificación contra el IL real de la versión 1.6.
- **Diferencia clave:** el original se adaptó aquí a las APIs vigentes de 1.6, sustituyendo los
  métodos obsoletos o eliminados y confirmando cada firma contra el ensamblado real del juego.

## Licencia del original

El repositorio original [bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish)
está publicado bajo la **Mozilla Public License 2.0 (MPL-2.0)**, © 2021 bradson
(licencia completa: [mozilla.org/MPL/2.0](https://mozilla.org/MPL/2.0/)).

Puntos que la MPL-2.0 exige y que conviene cumplir al publicar una obra que la usa como referencia:

1. **Atribución**: mantener el aviso de copyright y licencia del original (aquí queda documentado).
2. **Fuentes disponibles**: si se distribuye código cubierto por la MPL, el código fuente debe
   estar disponible bajo MPL-2.0, sin cargos más allá del coste de distribución.
3. **Aviso visible**: informar a quien reciba el mod de que el código fuente está gobernado por
   la MPL-2.0 y cómo obtenerla.

Este mod es una **reimplementación escrita desde cero** (no una copia textual del código del
original), por lo que las obligaciones se refieren sobre todo a la parte que desciende
conceptualmente del original y a la documentación de la atribución. Antes de publicar, revisa los
términos completos de la [MPL-2.0](https://mozilla.org/MPL/2.0/) y, si tienes dudas legales,
consulta a bradson directamente.
