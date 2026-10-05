# Credits and attribution / Créditos y atribución

> **EN:** This mod is a **from-scratch reimplementation** of the optimizations from the original
> **Performance Fish** mod, adapted to RimWorld 1.6. It is **not a copy of the original code**:
> every patch was rewritten and verified against the IL of Assembly-CSharp 1.6.9655. However, the
> **optimization catalog, the ideas and the cache architecture** come from the original, and the
> merit of conceiving them belongs to its author.
>
> **ES:** Este mod es una **reimplementación desde cero** de las optimizaciones del mod original
> **Performance Fish**, adaptada a RimWorld 1.6. **No es una copia del código original**: cada
> parche fue reescrito y verificado contra el IL de Assembly-CSharp 1.6.9655. Pero el **catálogo de
> optimizaciones, las ideas y la arquitectura de caches** provienen del original, y el mérito de
> concebirlas es de su autor.

## Original author / Autor original

| | |
|---|---|
| **Author / Autor** | bradson |
| **Original mod / Mod original** | Performance Fish |
| **Repository / Repositorio** | [github.com/bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish) |
| **Author's library / Biblioteca del autor** | [github.com/bbradson/Fishery](https://github.com/bbradson/Fishery) (Fishery, his modding library / su librería de modding) |

## Author of this version / Autor de esta versión

| | |
|---|---|
| **Author / Autor** | DGZ (Reforjed series / serie Reforjed) |
| **Repository / Repositorio** | `https://github.com/DGZ/Reforjed` (link the real repo when publishing / enlazar el repo real al publicar) |

## What was taken from the original and what is original here / Qué se tomó del original y qué es propio

- **EN — Taken as reference:** the *catalog* of optimizations (which methods deserve caching), the
  cache structures (by-reference, per-def/per-group indexes, parallel gas grids) and its
  "measure before optimizing" philosophy.
  **ES — Tomado como referencia:** el *catálogo* de optimizaciones (qué métodos merecen cacheo),
  las estructuras de caches (by-reference, índices por def/grupo, grids de gas paralelos) y su
  filosofía de "medir antes de optimizar".
- **EN — Written from scratch:** the C# code of this version, the IL rewrites via Prepatcher, the
  runtime hooks with Harmony, and the verification against the real IL of version 1.6.
  **ES — Escrito desde cero:** el código C# de esta versión, las reescrituras de IL vía Prepatcher,
  los enganches de runtime con Harmony, y la verificación contra el IL real de la versión 1.6.
- **EN — Key difference:** the original was adapted here to the current 1.6 APIs, replacing
  obsolete or removed methods and confirming every signature against the actual game assembly.
  **ES — Diferencia clave:** el original se adaptó aquí a las APIs vigentes de 1.6, sustituyendo
  los métodos obsoletos o eliminados y confirmando cada firma contra el ensamblado real del juego.

## License of the original / Licencia del original

**EN:** The original repository [bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish)
is published under the **Mozilla Public License 2.0 (MPL-2.0)**, © 2021 bradson
(full license: [mozilla.org/MPL/2.0](https://mozilla.org/MPL/2.0/)).

**ES:** El repositorio original [bbradson/Performance-Fish](https://github.com/bbradson/Performance-Fish)
está publicado bajo la **Mozilla Public License 2.0 (MPL-2.0)**, © 2021 bradson
(licencia completa: [mozilla.org/MPL/2.0](https://mozilla.org/MPL/2.0/)).

**EN:** Points the MPL-2.0 requires when publishing a work that uses it as a reference:
**ES:** Puntos que la MPL-2.0 exige al publicar una obra que la usa como referencia:

1. **Attribution / Atribución:** keep the original copyright and license notice (documented here / aquí queda documentado).
2. **Sources available / Fuentes disponibles:** if MPL-covered code is distributed, the source must be available under MPL-2.0, at no more than the distribution cost / si se distribuye código cubierto por la MPL, el código fuente debe estar disponible bajo MPL-2.0, sin cargos más allá del coste de distribución.
3. **Visible notice / Aviso visible:** inform recipients that the source is governed by the MPL-2.0 and how to obtain it / informar a quien reciba el mod de que el código fuente está gobernado por la MPL-2.0 y cómo obtenerla.

**EN:** This mod is a **from-scratch reimplementation** (not a textual copy of the original code),
so the obligations mainly concern the part that descends conceptually from the original and the
attribution documentation. Before publishing, review the full terms of the
[MPL-2.0](https://mozilla.org/MPL/2.0/) and, if you have legal doubts, contact bradson directly.

**ES:** Este mod es una **reimplementación escrita desde cero** (no una copia textual del código
del original), por lo que las obligaciones se refieren sobre todo a la parte que desciende
conceptualmente del original y a la documentación de la atribución. Antes de publicar, revisa los
términos completos de la [MPL-2.0](https://mozilla.org/MPL/2.0/) y, si tienes dudas legales,
consulta a bradson directamente.
