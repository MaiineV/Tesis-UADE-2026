# Ítems activos rediseñados

Documento de trabajo en estado **En revisión**. Estas fichas aplican la regla de resultado no autodestructivo: una tirada baja puede introducir caos, pérdida de control o una complicación ambiental, pero no puede quitarle directamente vida, escudo, daño, rolls, movimiento o una posición segura al jugador.

## Distribución de estructuras

| Estructura | Ítems |
| --- | --- |
| Bandas | Blood Transfusion, Justa de Justicia, Probability Drive |
| Binario significativo | Coin Shield |
| Gradiente de magnitud | Grapple Claw, Blood D6 |
| Jerarquía de resultados | Bottle'o Thunder |

---

## 1. Blood Transfusion — D10

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_BLOOD_TRANSFUSION` |
| Nombre visible | Blood Transfusion |
| Categoría funcional | Curación / Daño / Manipulación de estado |
| Familia de resolución | Bandas |
| Objetivo de diseño | Resolver una manipulación sanguínea rápida, sin abrir una fase de selección. La banda baja altera a todo el grupo enemigo; las bandas superiores drenan automáticamente al enemigo con mayor HP actual. |
| Dado propio | **D10** |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Activar el ítem durante el turno consume 1 roll y tira el D10 inmediatamente. |
| Target | Automático. En 4-10 selecciona al enemigo con mayor HP actual; los empates priorizan al más cercano al jugador. |
| Cantidad de targets | Un objetivo automático en las bandas mixta y positiva. La banda baja afecta a todos los enemigos elegibles de la sala. |
| Rango | Sala actual |
| Línea de visión | N/A; solo considera enemigos activos de la sala actual. |
| Selección previa | N/A; no abre modo de targeting. El enemigo que sería seleccionado se resalta al pasar el cursor sobre el ítem. |
| Cancelación | Permitida antes de activar el ítem; no después de consumir el roll. |
| Prerrequisitos | 1 roll disponible y al menos un enemigo vivo de la sala sin el tag `Bloodless`. |
| Resultado mínimo | Resultado 1: redistribuye la vida actual de los enemigos elegibles. Si solamente existe uno, le aplica 1 acumulación de Sangrado. |
| Resultado máximo | Resultado 10: el enemigo elegible con mayor HP actual recibe un daño igual a `A` y el jugador se cura por el daño efectivamente infligido. `A` es el valor máximo del dado de ataque más grande del jugador. |
| Regla de bandas | D10 dividido en 1-3, 4-7 y 8-10. Las bandas cambian cualitativamente el comportamiento de la transfusión. |
| Banda negativa | **1-3 — Redistribución sanguínea:** suma el HP actual de todos los enemigos elegibles no-jefe y lo reparte equitativamente, sin superar sus máximos. Puede curar enemigos heridos y dañar a los más saludables. |
| Banda mixta | **4-7 — Transfusión parcial:** el enemigo elegible con mayor HP actual recibe `max(1, floor(A × resultado / 10))` de daño y el jugador se cura por el 50% del daño real. |
| Banda positiva | **8-10 — Transfusión completa:** el enemigo elegible con mayor HP actual recibe `max(1, floor(A × resultado / 10))` de daño y el jugador se cura por el 100% del daño real. |
| Contrapartida | La banda baja puede restaurar enemigos que el jugador estaba por ejecutar o desarmar una estrategia de focus. Nunca modifica negativamente la vida del jugador. |
| Encantamiento compatible | Pool propia de Blood Transfusion; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | HP enemigo, daño, curación, Sangrado, resistencias y vida máxima. En la redistribución, los puntos sobrantes por redondeo se asignan de a uno entre enemigos que sigan debajo de su máximo. |
| Puede crear una tile | No |
| Tile creada | N/A |
| Duración del efecto | Instantáneo. Sangrado usa su duración estándar. |
| Inmunidades o restricciones | Los jefes y entidades `Bloodless` no participan de la redistribución. La curación no puede superar el daño real ni la vida máxima del jugador. |
| Costo IA | N/A; modifica valores de vida pero no crea una amenaza persistente. |
| Estados visuales | Reposo; preview automático del enemigo con mayor HP; tirada inmediata; red sanguínea para la banda baja; vínculo individual para las bandas mixta y positiva. |
| Casos especiales | Si la capacidad máxima conjunta impide repartir todo el HP, el excedente vuelve a distribuirse entre quienes sigan debajo de su máximo. Si hay un solo enemigo elegible, no se redistribuye y se le aplica Sangrado. |

---

## 2. Coin Shield — D4

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_COIN_SHIELD` |
| Nombre visible | Coin Shield |
| Categoría funcional | Defensa / Alteración global |
| Familia de resolución | Binario significativo |
| Objetivo de diseño | Apostar entre conservar el escudo propio o provocar una protección global caótica. El resultado bajo ayuda al jugador, pero también vuelve más resistentes a todos los enemigos. |
| Dado propio | **D4**, resuelto por paridad para representar una moneda sin introducir un D2 fuera del catálogo. |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Input del jugador durante su turno de combate |
| Target | Self; el resultado impar se expande automáticamente a todos los combatientes vivos. |
| Cantidad de targets | Resultado par: solo el jugador. Resultado impar: jugador y todos los enemigos vivos de la sala. |
| Rango | Sala actual |
| Línea de visión | N/A |
| Selección previa | N/A; solo se confirma la activación. |
| Cancelación | Permitida antes de confirmar; no después del pago. |
| Prerrequisitos | 1 roll, al menos 1 punto de escudo y una regla global de expiración de escudo definida. |
| Resultado mínimo | Resultado impar: todos los combatientes vivos ganan `E` de escudo, donde `E = max(1, ceil(50% del escudo actual del jugador))`. El escudo que el jugador ya tenía no se reduce. |
| Resultado máximo | Resultado par: conserva el 100% del escudo remanente hasta el siguiente turno del jugador. |
| Regla de bandas | N/A; usa un binario significativo por paridad del D4. Impar otorga escudo global y par estabiliza únicamente el escudo del jugador. |
| Banda negativa | N/A |
| Banda mixta | N/A |
| Banda positiva | N/A |
| Contrapartida | En el resultado impar, todos los enemigos vivos también reciben `E` de escudo. El jugador no pierde recursos, pero prolonga potencialmente el combate. |
| Encantamiento compatible | Pool propia de Coin Shield; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | Escudo, expiración, daño recibido y todos los combatientes vivos. `E` se calcula una sola vez usando el escudo del jugador al activar. |
| Puede crear una tile | No |
| Tile creada | N/A |
| Duración del efecto | El escudo global del resultado impar dura hasta ser consumido o hasta finalizar el siguiente turno de cada unidad afectada. El resultado par dura hasta finalizar el siguiente turno del jugador. |
| Inmunidades o restricciones | No puede usarse con 0 de escudo. Objetos, hazards y entidades no combatientes no reciben escudo. El bonus respeta el límite global de escudo de cada unidad. |
| Costo IA | N/A; no crea pickups ni modifica el pathfinding. |
| Estados visuales | Reposo; escudo del jugador resaltado; D4 girando como moneda; pulso dorado global en impar; cierre azul sobre el jugador en par. |
| Casos especiales | Si no hay enemigos vivos, el resultado impar todavía otorga `E` al jugador. Los enemigos invocados después de la resolución no reciben escudo retroactivamente. |

---

## 3. Grapple Claw — D6

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_GRAPPLE_CLAW` |
| Nombre visible | Grapple Claw |
| Categoría funcional | Movimiento / Control |
| Familia de resolución | Gradiente de magnitud |
| Objetivo de diseño | Usar la geometría de la sala para atraer enemigos o desplazarse hacia un anclaje. La tirada determina la distancia y los resultados bajos agregan una alteración secundaria de posicionamiento. |
| Dado propio | **D6** |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Input del jugador durante su turno de combate |
| Target | Un anclaje: enemigo, pared u obstáculo. |
| Cantidad de targets | Un anclaje principal. Con resultado 1-2 puede afectar a un enemigo secundario. |
| Rango | 6 tiles para adquirir el anclaje. El D6 determina entre 1 y 6 tiles de desplazamiento. |
| Línea de visión | Sí; trayectoria recta y despejada hasta el anclaje. |
| Selección previa | Elegir dirección y anclaje antes de pagar. La interfaz muestra la trayectoria y los enemigos próximos a la cadena. |
| Cancelación | Permitida antes de confirmar el anclaje; no después del pago. |
| Prerrequisitos | 1 roll, anclaje válido, trayectoria despejada y al menos 1 tile válida de desplazamiento. |
| Resultado mínimo | Resultado 1: desplaza 1 tile hacia el anclaje y activa Cadena Inestable. |
| Resultado máximo | Resultado 6: desplaza hasta 6 tiles hacia el anclaje o hasta quedar adyacente. |
| Regla de bandas | N/A; usa gradiente de magnitud. El resultado del D6 es la distancia máxima. Con 1-2 se agrega una complicación secundaria sin modificar al jugador. |
| Banda negativa | N/A |
| Banda mixta | N/A |
| Banda positiva | N/A |
| Contrapartida | Con 1-2, la cadena también arrastra 1 tile a un enemigo aleatorio próximo a su trayectoria. Puede agruparlo favorablemente o romper una formación preparada. |
| Encantamiento compatible | Pool propia de Grapple Claw; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | Movimiento, forced movement, obstáculos, enemigos, hazards y posición final. El jugador se detiene antes de una tile dañina. |
| Puede crear una tile | No |
| Tile creada | N/A |
| Duración del efecto | Instantáneo |
| Inmunidades o restricciones | Enemigos inmóviles o anclados no pueden ser atraídos, pero pueden funcionar como anclaje para mover al jugador. El enemigo secundario debe ser desplazable. |
| Costo IA | N/A; la IA reevalúa las posiciones mediante el sistema normal después de la resolución. |
| Estados visuales | Reposo; selección de anclaje; línea de gancho; preview de distancia; vibración de cadena para resultados 1-2; recorrido final. |
| Casos especiales | Contra una pared, el jugador termina en la última tile libre y segura. Contra un enemigo, este se detiene adyacente al jugador. Si no hay enemigo secundario, Cadena Inestable solamente produce feedback visual. |

---

## 4. Justa de Justicia — D12

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_JUSTA_DE_JUSTICIA` |
| Nombre visible | Justa de Justicia |
| Categoría funcional | Daño / Movimiento / Control |
| Familia de resolución | Bandas |
| Objetivo de diseño | Convertir una dirección elegida en una carga ofensiva. Las bandas determinan cuánto control conserva el jugador sobre el empuje y la potencia del impacto. |
| Dado propio | **D12** |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Input del jugador durante su turno de combate |
| Target | Dirección |
| Cantidad de targets | Un enemigo primario: el primero impactado. |
| Rango | Hasta 12 tiles. La distancia concreta depende de la banda y del resultado. |
| Línea de visión | Sí; el preview muestra obstáculos, primer enemigo y última tile segura. |
| Selección previa | Elegir y confirmar la dirección antes de pagar el roll. |
| Cancelación | Permitida antes de confirmar; no después del pago. |
| Prerrequisitos | 1 roll y al menos 1 tile libre y segura en la dirección elegida. |
| Resultado mínimo | Resultado 1: carga 1 tile. Si impacta, inflige 1 de daño y desplaza al enemigo a una tile adyacente válida aleatoria. |
| Resultado máximo | Resultado 12: carga hasta 12 tiles, inflige 12 de daño y empuja 2 tiles en la dirección elegida. |
| Regla de bandas | D12 dividido en 1-4, 5-8 y 9-12. La distancia y el daño usan el resultado obtenido; las bandas modifican el control del empuje. |
| Banda negativa | **1-4 — Carga turbulenta:** carga hasta el resultado, inflige ese valor como daño y expulsa al enemigo hacia una tile adyacente válida elegida al azar. |
| Banda mixta | **5-8 — Carga controlada:** carga hasta el resultado, inflige ese valor como daño y empuja 1 tile en la dirección elegida. |
| Banda positiva | **9-12 — Carga perfecta:** carga hasta el resultado, inflige ese valor como daño y empuja 2 tiles. Una colisión contra un obstáculo inflige daño adicional. |
| Contrapartida | La dirección queda fijada antes de tirar. En la banda baja, la colocación final del enemigo no está bajo control del jugador y puede romper una preparación táctica. |
| Encantamiento compatible | Pool propia de Justa de Justicia; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | Daño, movimiento, empuje, colisiones, obstáculos, bordes, hazards y posición final. |
| Puede crear una tile | No |
| Tile creada | N/A |
| Duración del efecto | Instantáneo |
| Inmunidades o restricciones | El jugador nunca atraviesa ni termina en una tile dañina. Enemigos inmunes al empuje reciben daño, pero permanecen en su posición. |
| Costo IA | N/A; solamente requiere recalcular posiciones después del movimiento. |
| Estados visuales | Reposo; flecha de dirección; preview de impacto; carga turbulenta en ámbar; controlada en rojo; perfecta en blanco/dorado. |
| Casos especiales | Si la dirección no contiene un enemigo, la acción conserva su valor de movilidad. Si no existe una tile válida de empuje, el enemigo recibe el daño pero no se desplaza. |

---

## 5. Probability Drive — D4

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_PROBABILITY_DRIVE` |
| Nombre visible | Probability Drive |
| Categoría funcional | Movimiento / Utilidad / Manipulación de posición |
| Familia de resolución | Bandas |
| Objetivo de diseño | Ofrecer un reposicionamiento fuerte cuya seguridad está garantizada, pero cuyo grado de precisión y efecto sobre la formación enemiga dependen del resultado. |
| Dado propio | **D4** |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Input del jugador durante su turno de combate |
| Target | Una tile central descubierta. |
| Cantidad de targets | El jugador. Con resultado 1 puede desplazar hasta 2 enemigos secundarios. |
| Rango | La tile central debe estar en la sala actual y a un máximo de 8 tiles. La banda define la región final. |
| Línea de visión | No; no puede seleccionar otra sala ni una zona no descubierta. |
| Selección previa | Elegir una tile central. La interfaz muestra el área y todos los destinos seguros posibles antes de pagar. |
| Cancelación | Permitida antes de confirmar; no después del pago. |
| Prerrequisitos | 1 roll, centro válido y al menos una tile de llegada segura disponible. |
| Resultado mínimo | Resultado 1: aterriza en una tile segura dentro de radio 1 y después intercambia las posiciones de hasta 2 enemigos elegibles del área. |
| Resultado máximo | Resultado 4: genera 3 destinos seguros dentro de radio 4 y permite que el jugador elija uno. |
| Regla de bandas | D4 dividido en resultado 1, resultados 2-3 y resultado 4. Las bandas representan distorsión, salto aleatorio y salto parcialmente controlado. |
| Banda negativa | **1 — Distorsión:** teletransporte seguro en radio 1. Después, dos enemigos elegibles del área intercambian posiciones aleatoriamente. |
| Banda mixta | **2-3 — Salto probabilístico:** teletransporte aleatorio uniforme entre las tiles seguras de radio 2 o 3. |
| Banda positiva | **4 — Control improbable:** el sistema sortea 3 tiles seguras diferentes dentro de radio 4 y el jugador elige una. |
| Contrapartida | La banda baja altera la formación enemiga después de determinar el destino y puede acercar amenazas o desarmar agrupaciones preparadas, sin causar daño inmediato. |
| Encantamiento compatible | Pool propia de Probability Drive; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | Teletransporte, ocupación de tiles, hazards, ataques telegrafiados, portales y forced movement enemigo. |
| Puede crear una tile | No |
| Tile creada | N/A |
| Duración del efecto | Instantáneo |
| Inmunidades o restricciones | Excluye destinos dañinos, ocupados, portales, salidas y tiles bajo ataques telegrafiados. Jefes, enemigos anclados e inmóviles no intercambian posición. |
| Costo IA | N/A; la IA recalcula sus decisiones con las nuevas posiciones. |
| Estados visuales | Reposo; centro seleccionado; destinos seguros; distorsión de enemigos; sorteo de destino; elección entre tres posibilidades en resultado 4. |
| Casos especiales | Con menos de dos enemigos elegibles, se intercambian solamente los disponibles o se omite esa parte; el teletransporte siempre se resuelve. Si hay una sola tile segura, se usa automáticamente. |

---

## 6. Blood D6 — D6

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_BLOOD_D6` |
| Nombre visible | Blood D6 |
| Categoría funcional | Daño / Distribución de daño |
| Familia de resolución | Gradiente de magnitud |
| Objetivo de diseño | Potenciar el siguiente combo sin reducir su daño base. Las caras bajas agregan poco daño y lo dispersan; las caras altas agregan más daño y lo concentran. |
| Dado propio | **D6** |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Input del jugador durante su turno de combate |
| Target | Self; modifica el próximo combo válido. |
| Cantidad de targets | El objetivo original del combo más hasta 5 enemigos secundarios. |
| Rango | N/A al activar. Al consumir el efecto, los objetivos secundarios deben estar a 4 tiles o menos del objetivo primario. |
| Línea de visión | N/A al activar. La dispersión solamente incluye enemigos con línea despejada desde el objetivo primario. |
| Selección previa | Confirmar que el próximo combo válido recibirá el modificador antes de pagar. El objetivo del combo se elige normalmente después. |
| Cancelación | Permitida antes de confirmar la activación; no después del pago. |
| Prerrequisitos | 1 roll, capacidad de ejecutar combos y ningún Blood D6 pendiente. |
| Resultado mínimo | Resultado 1: el objetivo recibe el 100% del daño normal. Se agrega 10% de daño, dividido entre hasta 6 enemigos elegibles. |
| Resultado máximo | Resultado 6: el objetivo recibe el 100% del daño normal más un 66% adicional concentrado sobre él. |
| Regla de bandas | N/A; usa gradiente. Bonus por cara: 10%, 20%, 30%, 40%, 50% y 66%. Máximo de receptores del bonus: 6, 5, 4, 3, 2 y 1. |
| Banda negativa | N/A |
| Banda mixta | N/A |
| Banda positiva | N/A |
| Contrapartida | Las caras bajas dispersan el bonus entre varios enemigos y pueden dificultar una estrategia de focus, aunque nunca reducen el daño normal al objetivo primario. |
| Encantamiento compatible | Pool propia de Blood D6; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | Daño de combo, críticos, multiplicadores, resistencias, objetivos secundarios y orden de operaciones. El daño base se aplica primero y el bonus se calcula después. |
| Puede crear una tile | No |
| Tile creada | N/A |
| Duración del efecto | Hasta el próximo combo válido del combate actual. Expira al finalizar el combate. |
| Inmunidades o restricciones | No se acumula ni puede reemplazarse mientras está pendiente. Cada enemigo recibe una sola porción del bonus. Sin secundarios válidos, todo el bonus se concentra en el primario. |
| Costo IA | N/A; produce daño instantáneo y no genera estados persistentes. |
| Estados visuales | Reposo; dado cargado sobre el próximo combo; preview de multiplicador; múltiples hilos de sangre en caras bajas; un único impacto concentrado en la cara 6. |
| Casos especiales | El bonus se divide de la forma más pareja posible. Los puntos sobrantes por redondeo se asignan primero al objetivo primario y luego a los enemigos más cercanos. Un combo inválido no consume el efecto. |

---

## 7. Bottle'o Thunder — D4

| Campo | Valor |
| --- | --- |
| ID propuesto | `ACTIVE_BOTTLE_O_THUNDER` |
| Nombre visible | Bottle'o Thunder |
| Categoría funcional | Control / Utilidad / Control de espacio |
| Familia de resolución | Jerarquía de resultados |
| Objetivo de diseño | Garantizar un aturdimiento básico y agregar objetivos con resultados superiores. La botella siempre modifica además el terreno mediante una complicación eléctrica visible. |
| Dado propio | **D4** |
| Costo de activación | 1 roll |
| Slot | Único slot de Ítem Activo |
| Trigger | Input del jugador durante su turno de combate |
| Target | Un enemigo primario. |
| Cantidad de targets | Entre 1 y 4 enemigos totales según el D4; incluye al objetivo primario. |
| Rango | 4 tiles para el objetivo primario y 2 tiles desde el objetivo anterior para cada rebote. |
| Línea de visión | Sí para el objetivo primario y para cada rebote. Un obstáculo sólido corta la cadena. |
| Selección previa | Elegir el objetivo primario y confirmar antes de pagar. La UI muestra los posibles candidatos de rebote, pero no la cadena final. |
| Cancelación | Permitida antes de confirmar; no después del pago. |
| Prerrequisitos | 1 roll y objetivo primario vivo, visible y vulnerable al aturdimiento. |
| Resultado mínimo | Resultado 1: aturde al objetivo primario durante 1 turno enemigo y crea 2 Charcos Eléctricos. |
| Resultado máximo | Resultado 4: aturde hasta 4 enemigos distintos durante 1 turno enemigo cada uno y crea 2 Charcos Eléctricos. |
| Regla de bandas | N/A; usa jerarquía. El resultado indica la cantidad máxima de objetivos totales: 1, 2, 3 o 4. Cada nivel conserva todos los efectos de los anteriores. |
| Banda negativa | N/A |
| Banda mixta | N/A |
| Banda positiva | N/A |
| Contrapartida | Los Charcos Eléctricos son neutrales y pueden afectar posteriormente al jugador o a un enemigo. Los rebotes eligen objetivos automáticamente y pueden distribuir el control de una forma no ideal. |
| Encantamiento compatible | Pool propia de Bottle'o Thunder; máximo 1 encantamiento. |
| Reemplazo | El nuevo encantamiento pisa al anterior. |
| Interacciones de resultado | Stun, turnos enemigos, rebotes, línea de visión, inmunidades, tiles eléctricas y pathfinding. Cada enemigo puede recibir un solo impacto por activación. |
| Puede crear una tile | Sí |
| Tile creada | `TILE_ELECTRIC_PUDDLE`: se genera en una tile válida próxima al objetivo primario. La primera unidad que entra recibe el efecto eléctrico y el charco desaparece. |
| Duración del efecto | Stun durante 1 turno del enemigo afectado. Los charcos duran 2 rondas o hasta activarse. |
| Inmunidades o restricciones | Los charcos no aparecen debajo de una unidad, no se activan al crearse y deben ser visibles. Enemigos inmunes a stun no pueden ser target ni candidatos de rebote. |
| Costo IA | Medio; enemigos y pathfinding deben evaluar los Charcos Eléctricos como hazards temporales. |
| Estados visuales | Reposo; objetivo primario; candidatos de rebote; cadena de 1-4 impactos; vidrio roto; preview y estado activo de los Charcos Eléctricos. |
| Casos especiales | Si hay menos enemigos que el máximo, la cadena termina después del último válido y conserva los stuns aplicados. Sin tiles válidas para los charcos, el aturdimiento y los rebotes se resuelven igualmente. |
