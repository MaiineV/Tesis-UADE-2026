# No revertir cambios del usuario

**Nunca descartes cambios del working tree que no hiciste vos.** Si aparece una
modificación que no venís de escribir en esta sesión, es trabajo del usuario
hasta que él diga lo contrario.

## Prohibido sin autorización explícita

- `git checkout -- <archivo>` / `git restore <archivo>`
- `git reset --hard`, `git clean`
- `git stash` para "sacar del medio"
- Borrar o sobrescribir archivos modificados que no creaste vos

Esto aplica igual si el cambio te parece ruido, churn del engine, o
irrelevante para tu tarea.

## Por qué

Un cambio sin commitear puede ser lo único que exista de ese trabajo. Un
`git checkout --` lo borra sin red: no está en ningún commit, no está en el
reflog, no hay forma de recuperarlo. El costo de dejar un diff de más es cero;
el de borrar trabajo ajeno es irreversible.

## Unity: el caso que más engaña

Unity escribe al disco cosas que el usuario editó en el editor y todavía no
guardó. Un reimport, un `AssetDatabase.Refresh()`, entrar a Play Mode o
cambiar un prefab pueden hacer que esas ediciones pendientes se vuelquen al
`.unity` o al `.prefab` de golpe.

Eso se **ve** como churn incidental —overrides de prefab instance, bloques de
plataforma en `.meta`, reserialización de tablas— pero puede ser trabajo del
usuario recién persistido. No lo revertas por más que no tenga nada que ver
con lo que estás tocando.

> Incidente que originó esta regla: se revirtió dos veces `02_Gameplay.unity`
> por "churn de reimport". Eran ajustes de layout que el usuario había hecho a
> mano sobre `RerollText` y `Canvas_Tooltip`. Se recuperaron a medias porque
> Unity todavía tenía parte en memoria; el resto hubo que reconstruirlo a mano
> desde el diff que quedó en el transcript. Con la ventana de Unity cerrada, se
> perdían.

## Qué hacer en cambio

1. Dejar el cambio donde está.
2. Nombrarlo al reportar: qué archivo, qué toca, y que no es tuyo.
3. Si ensucia el commit, `git add` selectivo de **tus** archivos — nunca
   `git add -A` a ciegas ni limpiar lo ajeno.
4. Si de verdad estorba, preguntar antes.

## Única excepción

Revertir algo que **vos** creaste en esta misma sesión y ya no va (ej. un
archivo temporal, un experimento descartado). Ante la duda de quién lo hizo,
tratalo como del usuario.
