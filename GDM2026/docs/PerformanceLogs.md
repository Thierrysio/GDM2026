# Interpréter les logs de performance Android

Les traces ci-dessous montrent plusieurs symptômes typiques d'un rendu d'interface bloqué sur un appareil Android :

```
I/OpenGLRenderer: Davey! duration=34245ms ...
I/OpenGLRenderer: Davey! duration=23467ms ...
I/anyname.gdm202: Waiting for a blocking GC Explicit
I/Choreographer: Skipped 38 frames!  The application may be doing too much work on its main thread.
```

## Signification

- **OpenGLRenderer "Davey!"** : Android indique qu'une image a pris >700 ms à être dessinée. Ici les durées de 23–34 s signalent un blocage massif du thread d'UI pendant le rendu.
- **Waiting for a blocking GC / concurrent copying GC** : le ramasse-miettes (“GC”) a dû libérer la mémoire et a temporairement suspendu l'application. Les paquets d'objets libérés (par ex. "5MB") et les pauses (par ex. "201ms") montrent un impact réel sur la réactivité.
- **Skipped N frames** : le Choreographer signale que le thread principal n'a pas rendu suffisamment vite, produisant un effet de lag ou gel visuel.

## Causes probables

- Travail intensif ou synchrone sur le thread principal (boucles lourdes, I/O bloquante, parsing volumineux).
- Allocation d'objets répétée provoquant des cycles de GC rapprochés.
- Phase de démarrage débordant la capacité de rendu (initialisation lourde, téléchargements, désérialisation JSON, etc.).

## Pistes de mitigation

- Déplacer les traitements lourds hors du thread principal (tâches asynchrones, `Task.Run`, services en arrière-plan).
- Réduire les allocations dans les sections critiques : réutiliser les buffers, éviter de recréer des objets à chaque frame.
- Décaler les chargements importants (images haute résolution, désérialisation) après l'affichage initial, ou les paginer.
- Activer le profilage de mémoire et du CPU (Android Studio Profiler) pour identifier les points chauds précis.
- Vérifier que les animations ou gradients complexes ne sont pas recalculés inutilement à chaque rendu.

En bref, ces logs indiquent que l'application bloque le thread d'interface et subit des pauses de GC prolongées; l'objectif est de rendre le travail plus asynchrone et de limiter les allocations afin de réduire les frames sautées.
