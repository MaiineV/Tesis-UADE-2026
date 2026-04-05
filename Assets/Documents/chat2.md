Si los dados más grandes:
* Reducen la probabilidad de conseguir combos
* Pero al mismo tiempo esos combos hacen más daño por ser más difíciles
  Entonces, ¿tiene sentido que además “cuesten más espacio” o sean más caros en términos de build?
  Siento que hay una posible contradicción o doble penalización, pero no termino de identificar si:
1. Es un problema real de balance
2. O si puede justificarse desde el diseño (risk vs reward, decisiones estratégicas, etc.)
   Me gustaría que analices:
* Si este sistema tiene coherencia interna
* Qué problemas de balance podría generar
* En qué casos tendría sentido que los dados más grandes ocupen más espacio
* Posibles alternativas de diseño para que la decisión sea más interesante (y no simplemente peor opción)

Show more
31 mar

Ran 5 commands, viewed a file
Ran 5 commands, viewed a file
Ahora tengo todo el contexto necesario. Acá va el análisis completo.

La pregunta central: ¿es doble penalización o diseño intencionado?
Tu intuición es correcta — hay una tensión real. Pero antes de diagnosticarla, hay que separar los dos tipos de "costo" que estás mezclando:

Costo 1 — Probabilístico: los dados grandes son más difíciles de combinar (range más amplio → menos chance de match). Costo 2 — Espacial: los dados grandes ocupan más slots del inventario (d10 = 2 espacios, d12 = 2, d20 = 3).

La pregunta es: ¿estos dos costos se compensan con las recompensas, o se suman de forma que los dados grandes nunca valgan la pena?

1. ¿El sistema tiene coherencia interna?
   Parcialmente sí. La tabla de tu documento ya revela el intento de equilibrar:

Dado	Espacios	EV	Mult.	Ratio EV/espacio
d6	1	3.5	×1.00	3.5
d8	1	4.5	×1.29	4.5 ← mejor ratio
d10	2	5.5	×1.57	2.75
d12	2	6.5	×1.86	3.25
d20	3	10.5	×3.00	3.5
El d8 es anomalía inmediata: mejor EV por espacio que el d6 sin ningún costo adicional. Tu propio documento ya lo marca con ⚠. Ese dado roto es el síntoma de que el sistema todavía no está balanceado — no el d10 o el d12.

El d20 en cambio tiene coherencia: ratio EV/espacio igual al d6, pero con volatilidad extrema y restricción de build severa (solo cabe con 2 dados chicos, lo que destruye la mayoría de combos). El espacio ahí sí tiene sentido narrativo y mecánico.

2. ¿Qué problemas de balance reales genera?
   Problema A — La doble penalización en el d10 y d12 es real.

Un jugador con d10 + d12 + d6 (3 dados, 5 espacios) tiene:

Menos combos disponibles (pierde Full House, Doble par a veces)
Mayor dificultad para conseguir cualquier combo (rangos amplios)
Y menos dados con los que trabajar
La "recompensa" es un multiplicador más alto, pero ese multiplicador se aplica solo cuando conseguís un combo. Si la probabilidad de combo cae más rápido que el multiplicador sube, el EV real de esa build puede ser peor que 5×d6.

Problema B — El multiplicador está diseñado sobre el EV promedio, no sobre los combos reales.

multiplicador_dado = EV_promedio_build ÷ 3.5

Esto es elegante matemáticamente, pero ignora que la frecuencia de combos no es lineal con el tamaño del dado. Un d12 tiene EV 6.5, pero la probabilidad de sacar un par entre dos d12 es 1/12 (8.3%) vs 1/6 (16.7%) entre dos d6. El multiplicador aumenta ×1.86 pero la chance de usar ese multiplicador cae más de la mitad. La fórmula sobrevalúa los dados grandes.

Problema C — El d8 domina silenciosamente.

Con 5×d8 conseguís todos los combos de un 5×d6 (misma cantidad de dados), pero con multiplicador ×1.29 y sin ningún costo extra de espacio. Mientras ese dado exista a 1 espacio, compite directamente con el d6 y lo hace obsoleto. Esto es lo que tu doc señala pero sin resolver todavía.

3. ¿Cuándo tiene sentido que los dados grandes ocupen más espacio?
   El sistema de espacios tiene sentido conceptual bajo una condición: que el costo espacial compre algo que no sea solo multiplicador.

Hay tres justificaciones válidas para el costo espacial:

Justificación 1 — Compra acceso a combos especiales. Si existiera un combo único que solo aparece con dados grandes (ej: "Crits" que solo d10+ pueden activar), el espacio tiene sentido porque abre una mecánica nueva, no solo más daño.

Justificación 2 — Compra escudo contra el sistema. Si los dados grandes tuvieran alguna forma de garantizar resultados (ej: encantamientos que solo aplican a dados de 2+ espacios), pagarías espacio para reducir la volatilidad. El Alchemist apunta en esta dirección.

Justificación 3 — Compra identidad de build. El d20 es el mejor ejemplo: ocupa 3 espacios, destruye los combos convencionales, y crea un archetype completamente distinto ("el apostador"). El costo espacial ahí no es penalización — es el precio de entrada a un estilo de juego único.

El problema es cuando el costo espacial solo compra multiplicador (d10, d12 sin más), porque entonces sí es doble penalización: menos dados + combos más difíciles + misma mecánica de siempre.

4. Alternativas de diseño para que la decisión sea más interesante
   Opción A — El espacio compra estabilidad, no multiplicador.

En vez de aumentar el multiplicador con dados grandes, que los dados grandes tengan un piso mínimo de resultado. Por ejemplo: el d10 siempre tira entre 4 y 10 (en lugar de 1-10). El jugador paga espacio para eliminar los resultados malos, no para inflar el EV promedio. Esto hace que builds con pocos dados grandes sean más predecibles aunque más caras en slots.

Opción B — Combos asimétricos por tamaño de dado.

Crear una categoría de combo que sea exclusiva de dados grandes. Por ejemplo, "Combo de potencia" que requiere mínimo 2 dados de 2+ espacios. Así el jugador que paga el costo espacial accede a un combo que los dados pequeños nunca pueden lograr, en lugar de competir en el mismo pool de combos pero desde peor posición.

Opción C — El costo espacial es dinámico (dados que crecen).

En lugar de que el d10 siempre ocupe 2 espacios, que empiece ocupando 1 espacio y crezca a 2 al encantarlo o al subirlo de nivel. El jugador elige cuándo "invertir" el espacio extra, y la recompensa es proporcional porque ya eligió cuando hacerlo.

Opción D — Resolver el d8 primero, antes de todo.

El problema más urgente es el d8, no el d10/d12. Si el d8 pasa a ocupar 2 espacios (o su multiplicador cae a ×1.10), el problema del ratio EV/espacio desaparece y los datos grandes recuperan su nicho natural sin cambiar nada más. A veces la mejor solución de balance es ajustar el outlier, no rediseñar el sistema.

Opción E — Separar multiplicador de probabilidad.

La raíz del problema es que la fórmula actual mezcla EV y probabilidad en un solo número. Una alternativa: que el multiplicador de daño venga del combo conseguido, no del dado. El dado grande aporta acceso a combos de alto daño, pero si no llegás al combo, el multiplicador es bajo. Así la recompensa es condicional (si armás el combo difícil, el pago es real) y no hay doble penalización porque el costo base no sube — solo el techo.

Resumen ejecutivo
El sistema tiene lógica interna, pero la doble penalización es real en el tramo medio (d10, d12). El d20 está bien justificado. El d8 está roto. La fórmula de multiplicador sobrevalúa el EV sin considerar la caída de frecuencia de combos.

La decisión de diseño más limpia en el corto plazo: resolver el d8 (subir a 2 espacios o bajar multiplicador) y darle a los dados grandes un diferenciador cualitativo (combo exclusivo, estabilidad de mínimo, o encantamiento especial) en lugar de solo más multiplicador. Eso convierte la elección de dado grande en una decisión estratégica genuina, no en una apuesta mal pagada.

Pero si el d12 ocupa 2 espacios. Que builds podria jugar que tengan chances de que salgan combos?

31 mar
Mirando los números concretos del documento:

Con d12 ocupando 2 espacios, tenés 3 espacios libres para completar la build. Tus opciones son:

Las builds viables con d12 (2 espacios)
d12 + 3×d4 → 5 dados, 5 espacios Esta es la más interesante. Tenés 5 dados, así que todos los combos están disponibles. Los d4 tienen rango 1-4, entonces si el d12 cae en 1, 2, 3 o 4, puede matchear con ellos. Probabilidad de par con el d12 contra un d4: 4/12 ≈ 33%. No es terrible. El arquetipo que proponés en el documento ("caótico — 1 dado potente + combos fáciles") tiene sentido real acá.

d12 + d4 + d6 + d4 → mismo caso, variante con d6 para ampliar un poco el rango intermedio.

2×d12 → 3 dados, 5 espacios Acá empieza el problema. Probabilidad de par entre dos d12: 1/12 ≈ 8%. Full House imposible con 3 dados (necesitás 5). Tu único combo real es Escalera (si caen consecutivos) o Par ocasional. Multiplicador alto, pero el combo sale rarísimo.

d12 + 2×d6 → 3 dados, 5 espacios Mismo problema estructural. El d12 casi nunca matchea con los d6 (solo en 1-6, o sea 50% del rango del d12). Par posible pero poco frecuente.

El problema de fondo que esto revela
Cuando el d12 ocupa 2 espacios, la única build que realmente funciona es d12 + 3 dados de 1 espacio. Y dentro de eso, los dados chicos que mejor le sirven son los de rango bajo (d3, d4) porque tienen más chance de matchear con las caras bajas del d12.

Eso genera una paradoja: para aprovechar el d12 (que en teoría es "daño bruto alto"), necesitás rodearlo de d3 y d4 que tiran bajo, lo que baja el multiplicador promedio de la build y anula parte del beneficio.

La decisión estratégica interesante existe, pero es más estrecha de lo que parece en papel.

¿Querés que calcule las probabilidades de combo concretas para d12 + 3×d4 vs 5×d6 para ver si el tradeoff está bien pagado o no?

si

31 mar
Voy a calcular esto en detalle.


Ran 2 commands
Ran 2 commands


Los números cuentan una historia bastante clara.

d12 + 3×d4 es la única build viable con d12, y aun así pierde −22.6% de EV contra el baseline. El resto de las builds con d12 caen entre −28% y −42%.

El problema central es este: el d12 destruye exactamente los combos que más daño hacen. El póker (22% de frecuencia, base 60) y el full house (20%, base 40) directamente desaparecen porque pasás de 5 dados a 4. El d12 sube la generala de 1.8% a 9.9%, sí — pero la generala en el baseline contribuye solo 1.8 de EV porque sale rara. Aunque la triplicás en frecuencia, el impacto real es menor que lo que perdés al resignar el póker.

Dicho esto, hay algo interesante en los datos que abre una puerta de diseño: d12 + 3×d4 tiene el trío más frecuente del sistema (42%), y eso conecta directamente con el Necromancer. Con la pasiva de trío ×2, esa build salta de EV 27.6 a 49.2 — todavía por debajo del 5×d6 con Necromancer (58.7), pero ya en el mismo rango competitivo y con un perfil completamente distinto.

Eso sugiere que el d12 no está pensado para ser una mejora general del d6. Está pensado para ser la pieza central de una build específica con una clase específica. Sin Necromancer, es objetivamente peor. Con Necromancer, tiene sentido. La pregunta de diseño que se abre es si eso es suficiente — o si el d12 debería tener al menos un contexto más donde sea viable.

Como podemos balancear esto entonces?

31 mar
Buena pregunta para atacar con datos antes de proponer soluciones. Déjame simular varias alternativas y comparar.


Ran 2 commands
Ran 2 commands


Los números son bastante contundentes. La Opción C — un bonus de ×1.4 al daño cuando los dados grandes consiguen un combo "fuerte" (trío o mejor) — es la única que resuelve el problema sin crear otros nuevos.

Por qué funciona y las otras no:

La A y la B atacan el lado equivocado del problema. El d12 no es débil porque su EV sea bajo — es débil porque sus combos salen poco. Subirlo a 2 espacios o ponerle un piso no cambia que el d12 casi nunca matchea con otros dados. La C en cambio amplifica exactamente lo que el d12 ya hace bien: sacar trío con los d4 que lo rodean.

Las consecuencias concretas de implementar la C:

d12 + 3×d4 queda en +1.4% del baseline. No es la build más fuerte, pero es competitiva y tiene una identidad clara: apostás a trío (42% de frecuencia) y cuando sale, el daño se dispara.

d12 + 3×d6 queda en −8%. Sigue siendo peor que el baseline, pero ya no es una trampa — es una decisión consciente de priorizar multiplicador alto sobre frecuencia de combo.

2×d12 + d6 y el d20 siguen siendo builds de alto riesgo y bajo EV. Eso tiene sentido narrativo: son las apuestas extremas del sistema, no opciones generales.

El Necromancer + d12+3×d4 con el bonus llega a +86% sobre el baseline. Es poderoso, pero es la combinación más específica del juego — requiere desbloquear Necromancer, elegir la build exacta, y que salga trío. El payout es proporcional a la especialización.

Una cosa que todavía queda pendiente: el d8 a 1 espacio sigue siendo +2.6% sobre el 5×d6 con los mismos combos disponibles y el mismo costo. No es urgente (es muy poco diferencial), pero si en algún momento querés que el d8 tenga su propio nicho en lugar de ser simplemente "d6 mejorado", la solución más limpia sería darle un encantamiento preinstalado en lugar de cambiarle el costo de espacio.

Tengo dos opciones:
-Ir en contra de todo y que los dados chicos sean los que ocupan MAS espacio

O

-Que los dados grandes (del d10 para adelante) puedan sumar por si solos entonces el jugador siempre tiene un daño base.

31 mar
Dos ideas muy distintas. Antes de simular, quiero pensar con vos el ángulo de diseño de cada una — porque los números solos no alcanzan acá.

Opción 1 — dados chicos ocupan más espacio

Esto invierte la lógica intuitiva del sistema y puede ser interesante exactamente por eso. El mensaje que le mandás al jugador cambia completamente: los dados chicos son "precisión cara" y los grandes son "poder barato". Pero hay un problema estructural: si el d3 y el d4 ocupan más espacio, las builds de 5 dados casi desaparecen del juego, porque para tener 5 dados necesitás 5 dados de 1 espacio. Los combos tipo póker y full house se vuelven rarísimos de armar. Básicamente rediseñás todo el árbol de builds desde cero.

Opción 2 — dados grandes tienen daño base garantizado

Esto ataca el problema real que encontramos: el d12 pierde EV porque sus combos salen poco. Si el d12 siempre aporta algo aunque no combine, la penalización de "salir sin combo" desaparece. Pero hay una pregunta de diseño clave antes de simular: ¿el daño base reemplaza el combo o se suma?

Eso cambia todo. Dame un segundo que lo simulo en ambos modos.


Ran 2 commands
Ran 2 commands
V

visualize
V

visualize
Parece que el visualizador está teniendo problemas técnicos ahora mismo. Te paso el análisis directo con los números.

Opción 1 — dados chicos caros
El problema no se resuelve, se mueve. El d8 queda igual que estaba (1 espacio, EV alto, todos los combos disponibles) y sigue siendo el dado dominante. Y aparece una rotura nueva que no existía: 2×d4 en 4 espacios genera +15% sobre el baseline, porque el rango 1–4 garantiza trío y generala con facilidad absurda. El d4 pasa de ser "el dado de combo fácil barato" a ser un arma rota en build pequeña, y deja de ser accesible en builds de 5 dados. Se replantea toda la lógica del árbol de progresión sin resolver el problema central.

Opción 2 — daño base garantizado
Acá hay tres variantes y no todas funcionan igual:

2A — el dado grande reemplaza el "nada": el efecto es invertido. d12+3×d4 casi nunca saca "nada" (solo 3% de las tiradas), así que el reemplazo no la ayuda casi nada. Beneficia sobre todo a d20+2×d6 y 2×d12+d6 que sacan "nada" el 41–42% de las veces — que son las builds más extremas, donde menos lo necesitás.

2B — el dado grande suma su valor × factor: el factor variable es el problema. Para subir d12+3×d4 al baseline necesitás factor 1.5, pero con ese factor 2×d12+d6 se dispara porque tiene dos dados sumando. No hay un número que equilibre ambas al mismo tiempo.

2C — bonus fijo por dado grande: esta es la que funciona. Los resultados con d10:+4 / d12:+7 / d20:+12 son:

Build	EV	vs baseline
5×d6	35.7	ref
d12 + 3×d4	34.6	−3.1%
d12 + 3×d6	32.8	−8.1%
2×d12 + d6	34.9	−2.4%
d20 + 2×d6	34.1	−4.5%
Subiendo un poco los valores (d12:+8 / d20:+14) todas las builds quedan entre −5% y +3%.

La conclusión entre las dos opciones
La Opción 1 reinventa la tabla de espacios sin resolver el desequilibrio del d8, y genera una rotura nueva con el d4 pequeño. Es un rediseño estructural que cambia el juego completo para no llegar a ningún lado mejor.

La Opción 2C es una adición localizada: los dados de d10 en adelante imprimen daño fijo cada turno. No toca la tabla de espacios, no toca los combos, no toca el multiplicador. El jugador entiende la propuesta de valor inmediatamente — "estos dados siempre hacen algo" — y el balance queda dentro de rangos razonables sin calibración compleja.

La única decisión que queda es si ese bonus fijo se muestra como número separado en la UI o se suma silenciosamente al total. Mostrarlo separado ("combo: 28 + bonus d12: 7 = 35") le da al jugador feedback claro de por qué eligió ese dado.