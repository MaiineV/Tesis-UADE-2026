# **Prototipo — Qué se espera obtener**

## **Alcance — qué debe funcionar**

### **Personaje**

El jugador controla un único personaje con tres stats operativas:

* **Vida** — puntos de vida totales. El jugador muere si llega a 0\.  
* **Velocidad** — stat que se suma al dado de huida para calcular el porcentaje de éxito.  
* **Destreza** — stat que se suma a los dados de arco, poción y forzar puerta para calcular porcentajes de acierto y curación.

No es necesario que las tres clases estén implementadas. Alcanza con un personaje base con stats fijas que permitan testear todas las mecánicas.

---

### **Build de dados**

Antes de iniciar la run, el jugador arma su bolsa de dados respetando el sistema de poder dadico:

* Cada dado tiene un costo: d6 \= 1pt, d8 \= 1.5pt, d10 \= 2pt, d12 \= 2.5pt.  
* El jugador tiene un presupuesto fijo (ej: 5 puntos) y no puede superarlo.  
* La pantalla de selección muestra los dados disponibles, su costo y el presupuesto restante en tiempo real.  
* Debe haber al menos 3 tipos de dados disponibles para elegir.

Este sistema es uno de los diferenciales del juego y debe estar funcional desde el prototipo.

---

### **Exploración — estructura del piso**

El piso se genera de forma procedural al iniciar la run. La estructura mínima esperada es:

* Entre 8 y 14 salas conectadas en una grilla tipo Isaac.  
* Tipos de sala presentes: combate (mayoría), tienda (1), poción (1), boss (1).  
* El jugador puede moverse libremente entre salas usando las puertas.  
* Un **minimapa** visible en el HUD que muestra las salas descubiertas y las adyacentes. Solo tres tipos de sala muestran inicial: **T** (tienda), **B** (boss), **P** (poción).  
* El piso termina al derrotar al boss de la sala B.

---

### **Turno del jugador — exploración**

Fuera del combate, el jugador elige **una de tres acciones** por turno. Elegir cualquiera pasa el turno y los enemigos se mueven:

**1\. Moverse** Tira su dado de velocidad y avanza esa cantidad de casillas en la sala.

**2\. Usar el Arco** Selecciona una casilla dentro de un área de 5×5 centrada en su posición. Si hay un enemigo en esa casilla se resuelve el disparo:

* Se tira un d20, se suma Destreza.  
* El resultado se convierte en porcentaje de acierto: `(resultado / (20 + Destreza_max)) × 100`.  
* Se genera un número aleatorio entre 1 y 100\. Si es ≤ al porcentaje → impacta. Si es \> → falla.  
* El arco no está disponible dentro del combate por turnos.

**3\. Tomar la Poción**

* Se tira un dado (d10), se suma Destreza.  
* El resultado se convierte en porcentaje de curación sobre la vida máxima: `(resultado / (d10_max + Destreza_max)) × 100`.  
* No hay fallo — siempre cura algo. La cantidad varía según el resultado.  
* La poción tiene un solo uso por run. Se recarga visitando la sala de Poción.

---

### **Iniciación del combate**

El combate comienza cuando un enemigo alcanza la casilla del jugador. Los enemigos se mueven usando su propio dado de movimiento, avanzando hacia el jugador cada turno. El combate es por turnos.

---

### **Turno del jugador — combate**

El jugador dispone de **3 tiradas estilo Generala** para construir su ataque:

1. Lanza todos sus dados.  
2. Reserva los que quiere conservar y relanza el resto (hasta 2 veces más).  
3. Con el resultado final arma sus combinaciones de ataque.

**Combinaciones de ataque disponibles:**

| Combinación | Descripción |
| ----- | ----- |
| **Par** | 2 dados iguales |
| **Pierna** | 3 dados iguales |
| **Escalera** | Secuencia numérica |
| **Full House** | Trío \+ par |
| **Póker** | 4 dados iguales |
| **Generala** | 5 dados iguales |
| **Generala Doble** | 2da Generala en el mismo combate |
| **Dado más alto** | Sin combo — toma el valor del dado más alto |

Las tiradas no usadas para atacar se usan como **defensa**: el jugador relanza esos dados y los resultados bloquean parte del daño entrante. El mecanismo exacto de conversión de defensa a bloqueo debe estar definido antes de implementar.

---

### **Barra de energía y modo Craps**

Durante el combate se llena una barra de energía. Al llenarse se activa el **modo Craps**:

* El jugador apuesta qué combinación le saldrá en la siguiente tirada.  
* Si acierta → bonificación de daño o efecto especial.  
* Si falla → penalización (debuff, reducción de daño, o turno perdido).

---

### **Turno del enemigo**

* El enemigo realiza una única tirada que determina su daño (ritmo ágil, sin esperas largas).  
* Los enemigos también tienen barra de energía propia. Al llenarse, su siguiente ataque tiene mayor probabilidad de hacer el doble de daño.

---

### **Enemigo arquero**

Nuevo tipo de enemigo con IA de rango:

* Mantiene una distancia mínima de N casillas del jugador. Desde ahí dispara.  
* Si el jugador se acerca demasiado (1×1), en el siguiente turno huye para recuperar distancia.  
* Su disparo se resuelve con un dado \+ stat de Puntería convertido a porcentaje, igual que el arco del jugador.

---

### **Combate doble**

Cuando el jugador está en combate y hay otro enemigo en la sala:

* Ese segundo enemigo avanza **1 casilla por turno completo** (ataque \+ defensa \+ ataque enemigo) hacia el jugador, sin importar si es cuerpo a cuerpo o arquero.  
* Al llegar, se activa el combate doble: el jugador elige a cuál de los dos atacar cada turno.  
* Ambos enemigos atacan al jugador en sus respectivos turnos.  
* El combate termina solo cuando ambos enemigos están derrotados.

---

### **Huida**

Disponible en cualquier turno del jugador durante el combate:

* Se tira un dado de huida \+ Velocidad → porcentaje de éxito.  
* Se genera un número aleatorio entre 1 y 100\. Si es ≤ al porcentaje → huida exitosa.  
* **Si exitosa:** se cobra automáticamente el 10% de la vida máxima. Se tira el dado de movimiento automáticamente y el jugador se aleja de los enemigos. Abandona el estado de combate.  
* **Si falla:** el turno se consume. Sin penalización de vida.

---

### **Forzar puerta**

Si el jugador se posiciona sobre una casilla de puerta, en su próximo turno puede elegir forzarla:

* Se tira un dado \+ Destreza → porcentaje de éxito.  
* **Si exitoso:** el jugador pasa a la sala siguiente. Los enemigos de la sala actual quedan vivos pero reciben una reducción de vida fija (25% propuesto).  
* **Si falla:** turno consumido, el jugador no avanza.

---

### **Persistencia de enemigos**

Si el jugador sale de una sala sin haber derrotado a todos los enemigos y vuelve a entrar:

* Los enemigos vivos reaparecen con **la vida que tenían al salir**.  
* Su posición en la sala es aleatoria — no la que tenían antes.  
* Los enemigos muertos no reaparecen.

---

### **Sala de Tienda**

La tienda es funcional para el prototipo con contenido mínimo:

* Ítems distribuidos en el suelo con texto visible que indica nombre y precio.  
* Al acercarse aparece la descripción completa y un botón de compra.  
* El jugador compra con el Oro acumulado durante la run.  
* **Calibración de precios:** ítem estándar ≈ recompensa de 3-4 enemigos normales. Ítem premium ≈ 5 enemigos.  
* Los ítems no comprados persisten si el jugador sale y vuelve.

---

### **Economía de Oro**

* Los enemigos sueltan Oro al morir — **no mejoras de dado**.  
* El Oro se suma automáticamente al inventario del jugador al terminar el combate.  
* El total de Oro acumulado es visible en el HUD en todo momento.

