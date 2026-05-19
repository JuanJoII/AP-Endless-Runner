# Documentación Técnica: Sistema de Audio Procedural

**Integrantes:** [Juan José Camacho](https://github.com/JuanJoII), [Stefany López](https://github.com/StefanyLopez)

**Curso:** Videojuegos Móviles

**Fecha:** 19 de mayo de 2026

**Corte:** Tercer Corte

---

## 1. Descripción General
La propuesta sonora para este Endless Runner se aleja de los samples pregrabados para centrarse en un sistema de **síntesis procedural en tiempo real**. El objetivo estético es capturar la esencia de un "gato de callejón" mediante una estética **funk/blues urbana**. 

El sistema utiliza osciladores puros para generar cada sonido, permitiendo una retroalimentación dinámica que responde a la velocidad del juego y a las acciones del usuario sin perder calidad ni ocupar espacio excesivo en memoria.

## 2. Arquitectura de Implementación

A continuación se muestra la estructura de los archivos que componen el sistema de audio procedural y su ubicación en el proyecto:

```text
Assets/Scripts/
├── GameManager/
│   ├── RunnerAudioManager.cs   <-- Administrador central y pool de SFX
│   ├── RunnerSynthAmbient.cs    <-- Generador de música (Melodía, Bajo, Pad)
│   └── RunnerUISounds.cs       <-- Scripts para eventos de botones y UI
├── Sounds/
│   └── NewAudio/
│       ├── SynthSFX.cs         <-- Motor de síntesis pura (Osciladores)
│       └── DrumMachine.cs      <-- Sintetizador de percusión (Kick, Snare, HiHat)
└── Characters/
    └── CharacterInputController.cs  <-- Disparador de sonidos (Salto, Slide)
```

### Descripción de Componentes

*   **SynthSFX.cs (Motor de Síntesis)**: Es la base del sistema. Genera ondas matemáticas (senoidales, cuadradas, FM, etc.) en tiempo real mediante el método `OnAudioFilterRead`, eliminando la necesidad de archivos `.wav`.
*   **RunnerAudioManager.cs (Administrador)**: Singleton que centraliza el disparo de efectos. Gestiona un pool circular de sintetizadores para permitir polifonía y configura los parámetros de síntesis para cada acción del juego.
*   **RunnerSynthAmbient.cs (Banda Sonora)**: Orquestador de la música generativa. Maneja las capas armónicas y rítmicas sincronizadas a 130 BPM bajo la escala de C Minor Blues.
*   **DrumMachine.cs (Percusión)**: Módulo especializado en generar sonidos de batería (bombo, redoblante, platillos) mediante síntesis sustractiva y de ruido.
*   **RunnerUISounds.cs (Interacción)**: Script puente que permite asignar funciones de sonido (Click, Pausa, Compra) directamente a los eventos de la interfaz de Unity.

---

## 3. Tabla de Sonidos Implementados

| Evento / Función | Técnica de Síntesis | Justificación Estética / Perceptual |
| :--- | :--- | :--- |
| **Salto (Jump)** | Aditiva (con sweep) | Barrido ascendente rápido que refuerza la sensación de impulso. |
| **Deslizamiento (Slide)** | FM synthesis | Sonido de fricción aireada (whoosh) que persiste mientras el gato está agachado. |
| **Monedas (Coin)** | Aditiva + Sine | Ping metálico agudo y corto que comunica recompensa inmediata. |
| **Imán (Magnet)** | FM agresiva | Pulso eléctrico tipo "chispa" para feedback táctil de atracción. |
| **Multiplicador x2** | Arpegio (C-E-G-C) | Secuencia musical ascendente que transmite éxito y mejora. |
| **Estrella (Invencible)** | Aditiva (Sweep) | Ráfaga brillante y potente para denotar invulnerabilidad. |
| **Choque (Hit)** | FM + Sine | Impacto disonante con sub-bajo para dar peso físico al error. |
| **UI Click (`PlayClick`)** | Sine (680Hz) | Tono suave y neutro para navegación básica por menús. |
| **UI Confirmar (`PlayConfirm`)** | Aditiva + Sweep | Brillo ascendente para feedback positivo de selección. |
| **UI Cancelar (`PlayCancel`)** | Square Wave | Tono descendente y seco para indicar retroceso o cierre. |
| **UI Compra (`PlayPurchase`)** | Arpegio Major | Secuencia de 3 notas festivas para validar transacciones. |
| **UI Error (`PlayError`)** | FM Disonante | Sonido metálico "sucio" para indicar falta de recursos o error. |
| **UI Pausa (`PlayPause`)** | Sine (440Hz) | Tono puro "clink" para marcar la detención del flujo de juego. |
| **UI Inicio (`PlayGameStart`)** | Aditiva + Sweep | Sonido épico ascendente que prepara al jugador para la carrera. |

---

## 4. Explicación Técnica de Parámetros

### Síntesis de Bajo (Funk Bass)
Para el bajo, se utilizó una **Square Wave** (onda cuadrada) para el ataque "funk" característico, mezclada con un **Saw Sub** para dar cuerpo en las frecuencias graves. Se aplicó una envolvente ADSR con ataque casi instantáneo (0.003s) para dar "punch".

### Melodía (Blues Note)
La melodía utiliza una síntesis aditiva de **5 armónicos impares**, emulando la riqueza de un instrumento de viento o un sintetizador analógico clásico. La variación procedural se logra mediante un ligero vibrato gestionado por un LFO en el hilo de audio.

### Sistema de Pool
Se implementó un pool circular de 6 slots para efectos de sonido (*one-shots*), lo que permite que sonidos rápidos (como recoger varias monedas) se superpongan sin interrumpir las colas de release de los sonidos anteriores.

---

## 5. Música de Fondo
La música es un sistema generativo de 4 compases:
*   **Tempo**: 130 BPM (constante).
*   **Escala**: Do Menor Blues (C Minor Blues).
*   **Estructura**:
    *   **Compás 3 (Blue Note)**: Inserción del Fa# (F#4) para crear la tensión armónica típica del jazz/blues.
    *   **Bajo**: Progresión Cm7 - Fm7 - G7 - Cm7 ejecutada por un *square bass* con ritmo walking.
    *   **Batería**: Patrón funk sincopado con Hi-Hats en corcheas constantes para mantener la adrenalina del Runner.

---

## 6. Conclusiones y Análisis
La implementación de audio procedural transformó el proyecto de una demo estática a una experiencia inmersiva coherente. La mayor dificultad fue la sincronización de hilos para evitar errores de desbordamiento, lo cual se resolvió mediante la unificación de los arrays de duración y frecuencia. El resultado es un sistema ligero, reactivo y con una identidad sonora única que se adapta al ritmo visual del juego.
