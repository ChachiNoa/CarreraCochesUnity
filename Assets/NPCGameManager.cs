using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona la puntuación del juego y las pantallas cómicas de fin de partida.
/// Utiliza OnGUI para garantizar un renderizado 100% infalible en cualquier versión de Unity.
/// Soporta el sonido de la bocina al mantener Espacio y asustar a los peatones cercanos.
/// Incluye sonidos de victoria/derrota, sonido al aparecer la pantalla final,
/// y un mensaje especial cómico cuando el contador de asustados llega a 10.
/// </summary>
public class NPCGameManager : MonoBehaviour
{
    public static NPCGameManager Instance { get; private set; }

    [Header("Ajustes del Juego")]
    [Tooltip("Límite de peatones para ganar o perder la partida.")]
    public int limiteObjetivo = 10;

    // ─────────────────────────────────────────────
    // SONIDOS DE BOCINA (ESPACIO)
    // ─────────────────────────────────────────────
    [Header("Sonido de Bocina (mantener Espacio)")]
    [Tooltip("Clip de audio que sonará en BUCLE mientras mantienes pulsada la barra espaciadora.")]
    public AudioClip sonidoClaxon;
    [Tooltip("Distancia máxima a la que la bocina asustará a los peatones cercanos.")]
    public float rangoBocina = 12.0f;

    // ─────────────────────────────────────────────
    // SONIDOS DE FIN DE PARTIDA
    // ─────────────────────────────────────────────
    [Header("Sonidos de Fin de Partida")]
    [Tooltip("Sonido que suena cuando GANAS (atropellas 10 peatones).")]
    public AudioClip sonidoVictoria;
    [Tooltip("Sonido que suena cuando PIERDES (10 peatones cruzan sanos).")]
    public AudioClip sonidoDerrota;
    [Tooltip("Sonido que suena cuando el contador de ASUSTADOS llega a 10.")]
    public AudioClip sonidoAsustados;
    [Tooltip("Sonido que suena EN EL MOMENTO en que aparece la pantalla de fin (si lo dejas vacío se usará el de victoria/derrota).")]
    public AudioClip sonidoPantallaFin;

    // ─────────────────────────────────────────────
    // ESTADO INTERNO
    // ─────────────────────────────────────────────
    private int peatonesAtropellados = 0;
    private int peatonesVivos        = 0;
    private int peatonesAsustados    = 0;
    private bool juegoTerminado      = false;
    private bool sonidoFinReproducido = false;

    // Tipo de resultado para saber qué texto/color mostrar
    private enum TipoFin { Ninguno, Victoria, Derrota, Asustados }
    private TipoFin tipoFin = TipoFin.Ninguno;

    // AudioSources
    private AudioSource claxonAudioSource;   // loop del claxon
    private AudioSource finAudioSource;      // sonidos de fin de partida

    // Caché de texturas
    private Texture2D texturaHUD;
    private Texture2D texturaFondoGO;
    private Texture2D texturaBtnNormal;
    private Texture2D texturaBtnHover;
    private Texture2D texturaBtnActive;

    // ─────────────────────────────────────────────────
    // AWAKE
    // ─────────────────────────────────────────────────
    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        Time.timeScale = 1f;

        // Texturas UI
        texturaHUD       = CrearTexturaColor(new Color(0.07f, 0.07f, 0.07f, 0.85f));
        texturaFondoGO   = CrearTexturaColor(new Color(0.04f, 0.04f, 0.04f, 0.95f));
        texturaBtnNormal = CrearTexturaColor(new Color(0.12f, 0.45f, 0.9f));
        texturaBtnHover  = CrearTexturaColor(new Color(0.2f,  0.55f, 1f));
        texturaBtnActive = CrearTexturaColor(new Color(0.08f, 0.35f, 0.75f));

        // AudioSource para el claxon (loop)
        claxonAudioSource            = gameObject.AddComponent<AudioSource>();
        claxonAudioSource.playOnAwake = false;
        claxonAudioSource.loop        = true;

        // AudioSource para sonidos de fin (no loop)
        finAudioSource            = gameObject.AddComponent<AudioSource>();
        finAudioSource.playOnAwake = false;
        finAudioSource.loop        = false;
    }

    // ─────────────────────────────────────────────────
    // UPDATE
    // ─────────────────────────────────────────────────
    void Update()
    {
        if (juegoTerminado)
        {
            // Paramos el claxon si el juego terminó
            if (claxonAudioSource.isPlaying) claxonAudioSource.Stop();
            return;
        }

        // Sincronizamos el clip del claxon si ha cambiado en el Inspector
        if (claxonAudioSource.clip != sonidoClaxon)
            claxonAudioSource.clip = sonidoClaxon;

        // ─── CONTROL DEL CLAXON ───────────────────
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (sonidoClaxon != null && !claxonAudioSource.isPlaying)
                claxonAudioSource.Play();
        }

        if (Input.GetKey(KeyCode.Space))
        {
            ScannearYAsustarPeatones();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            if (claxonAudioSource.isPlaying)
                claxonAudioSource.Stop();
        }
    }

    // ─────────────────────────────────────────────────
    // LÓGICA DEL CLAXON
    // ─────────────────────────────────────────────────
    private void ScannearYAsustarPeatones()
    {
        Transform transformCoche = null;
        CarControl coche = FindObjectOfType<CarControl>();
        if (coche != null)
        {
            transformCoche = coche.transform;
        }
        else
        {
            GameObject playerObj = GameObject.FindWithTag("Player");
            if (playerObj != null) transformCoche = playerObj.transform;
        }

        if (transformCoche == null) return;

        SimpleNPCWalker[] peatones = FindObjectsOfType<SimpleNPCWalker>();
        foreach (SimpleNPCWalker peaton in peatones)
        {
            if (Vector3.Distance(transformCoche.position, peaton.transform.position) <= rangoBocina)
                peaton.Asustar();
        }
    }

    // ─────────────────────────────────────────────────
    // REGISTRO DE EVENTOS
    // ─────────────────────────────────────────────────
    public void RegistrarPeatonAtropellado()
    {
        if (juegoTerminado) return;
        peatonesAtropellados++;
        if (peatonesAtropellados >= limiteObjetivo)
        {
            tipoFin = TipoFin.Victoria;
            TerminarJuego();
        }
    }

    public void RegistrarPeatonCruzado()
    {
        if (juegoTerminado) return;
        peatonesVivos++;
        if (peatonesVivos >= limiteObjetivo)
        {
            tipoFin = TipoFin.Derrota;
            TerminarJuego();
        }
    }

    public void RegistrarPeatonAsustado()
    {
        if (juegoTerminado) return;
        peatonesAsustados++;
        if (peatonesAsustados >= limiteObjetivo)
        {
            tipoFin = TipoFin.Asustados;
            TerminarJuego();
        }
    }

    // ─────────────────────────────────────────────────
    // FIN DE PARTIDA
    // ─────────────────────────────────────────────────
    private void TerminarJuego()
    {
        juegoTerminado = true;
        Time.timeScale = 0f;

        // Seleccionamos qué clip reproducir
        AudioClip clipFin = null;

        // Si hay sonido específico para la pantalla de fin, lo usamos como "aparición"
        if (sonidoPantallaFin != null)
            clipFin = sonidoPantallaFin;
        else
        {
            // Si no, usamos el sonido correspondiente al resultado
            switch (tipoFin)
            {
                case TipoFin.Victoria:   clipFin = sonidoVictoria;  break;
                case TipoFin.Derrota:    clipFin = sonidoDerrota;   break;
                case TipoFin.Asustados:  clipFin = sonidoAsustados; break;
            }
        }

        if (clipFin != null && !sonidoFinReproducido)
        {
            sonidoFinReproducido = true;
            // Time.timeScale = 0, así que usamos PlayClipAtPoint con Time.unscaledDeltaTime
            // PlayClipAtPoint no depende de timeScale, es perfecto para esto
            AudioSource.PlayClipAtPoint(clipFin, Camera.main != null ? Camera.main.transform.position : Vector3.zero);
        }
    }

    private void ReiniciarJuego()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    // ─────────────────────────────────────────────────
    // GUI
    // ─────────────────────────────────────────────────
    void OnGUI()
    {
        // ══════════════════════════════════════════════
        // 1. HUD (MARCADORES)
        // ══════════════════════════════════════════════
        GUIStyle estiloHUD = new GUIStyle(GUI.skin.box);
        estiloHUD.normal.background = texturaHUD;

        // Caja del HUD: más alta para albergar 3 contadores + el mensaje especial si aplica
        float alturaHUD = (peatonesAsustados >= limiteObjetivo && juegoTerminado) ? 145 : 110;
        GUI.Box(new Rect(20, 20, 340, alturaHUD), "", estiloHUD);

        GUIStyle estiloAtropellados = new GUIStyle(GUI.skin.label);
        estiloAtropellados.fontSize  = 16;
        estiloAtropellados.fontStyle = FontStyle.Bold;
        estiloAtropellados.normal.textColor = new Color(1f, 0.3f, 0.3f);

        GUIStyle estiloVivos = new GUIStyle(GUI.skin.label);
        estiloVivos.fontSize  = 16;
        estiloVivos.fontStyle = FontStyle.Bold;
        estiloVivos.normal.textColor = new Color(0.3f, 1f, 0.3f);

        GUIStyle estiloAsustados = new GUIStyle(GUI.skin.label);
        estiloAsustados.fontSize  = 16;
        estiloAsustados.fontStyle = FontStyle.Bold;
        estiloAsustados.normal.textColor = new Color(1f, 0.65f, 0.15f);

        GUI.Label(new Rect(35, 27,  310, 25), $"Personas atropelladas: {peatonesAtropellados} / {limiteObjetivo}", estiloAtropellados);
        GUI.Label(new Rect(35, 55,  310, 25), $"Personas vivas: {peatonesVivos} / {limiteObjetivo}",               estiloVivos);
        GUI.Label(new Rect(35, 83,  310, 25), $"Personas asustadas: {peatonesAsustados} / {limiteObjetivo}",       estiloAsustados);

        // ──────────────────────────────────────────────
        // MENSAJE ESPECIAL al llegar a 10 asustados
        // (se muestra incluso antes de que termine el juego)
        // ──────────────────────────────────────────────
        if (peatonesAsustados >= limiteObjetivo)
        {
            GUIStyle estiloMensajeEspecial = new GUIStyle(GUI.skin.label);
            estiloMensajeEspecial.fontSize        = 13;
            estiloMensajeEspecial.fontStyle       = FontStyle.BoldAndItalic;
            estiloMensajeEspecial.wordWrap        = true;
            estiloMensajeEspecial.normal.textColor = new Color(1f, 0.85f, 0.0f); // Amarillo dorado
            GUI.Label(new Rect(35, 108, 310, 35),
                "Como te gustan los coches de choche ehhh...",
                estiloMensajeEspecial);
        }

        // ══════════════════════════════════════════════
        // 2. PANTALLA DE FIN DE PARTIDA
        // ══════════════════════════════════════════════
        if (!juegoTerminado) return;

        // Fondo oscuro translúcido de toda la pantalla
        GUIStyle estiloFondoGO = new GUIStyle(GUI.skin.box);
        estiloFondoGO.normal.background = texturaFondoGO;
        GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", estiloFondoGO);

        string titulo       = "";
        Color  colorTitulo  = Color.white;
        string subtitulo    = "";
        string mensajeExtra = "";

        switch (tipoFin)
        {
            case TipoFin.Victoria:
                titulo       = "¡ERES UN BUEN CONDUCTOR!";
                colorTitulo  = new Color(0.3f, 1f, 0.3f);
                subtitulo    = "¡Lograste atropellar a 10 peatones! Eres el rey absoluto del asfalto.";
                break;

            case TipoFin.Asustados:
                titulo       = "¡CONDUCES DE FORMA PECULIAR!";
                colorTitulo  = new Color(1f, 0.65f, 0.15f);
                subtitulo    = "¡Y se nota que te gustan mucho los coches de choque!";
                mensajeExtra = "Como te gustan los coches de choche ehhh...";
                break;

            case TipoFin.Derrota:
            default:
                titulo       = "¡ERES UN MAL CONDUCTOR!";
                colorTitulo  = new Color(1f, 0.3f, 0.3f);
                subtitulo    = "Dejaste cruzar a 10 peatones sanos y salvos... ¿Dónde quedó tu furia al volante?";
                break;
        }

        // ── Estilos de texto fin ────────────────────
        GUIStyle estiloTituloGO = new GUIStyle(GUI.skin.label);
        estiloTituloGO.fontSize   = 46;
        estiloTituloGO.fontStyle  = FontStyle.Bold;
        estiloTituloGO.alignment  = TextAnchor.MiddleCenter;
        estiloTituloGO.wordWrap   = true;
        estiloTituloGO.normal.textColor = colorTitulo;

        GUIStyle estiloSubtituloGO = new GUIStyle(GUI.skin.label);
        estiloSubtituloGO.fontSize  = 22;
        estiloSubtituloGO.alignment = TextAnchor.MiddleCenter;
        estiloSubtituloGO.wordWrap  = true;
        estiloSubtituloGO.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

        GUIStyle estiloMensajeGO = new GUIStyle(GUI.skin.label);
        estiloMensajeGO.fontSize        = 26;
        estiloMensajeGO.fontStyle       = FontStyle.BoldAndItalic;
        estiloMensajeGO.alignment       = TextAnchor.MiddleCenter;
        estiloMensajeGO.wordWrap        = true;
        estiloMensajeGO.normal.textColor = new Color(1f, 0.85f, 0.0f); // Amarillo dorado

        // ── Posiciones ──────────────────────────────
        float centerY = Screen.height / 2f;

        GUI.Label(new Rect(0, centerY - 150, Screen.width, 80), titulo,    estiloTituloGO);
        GUI.Label(new Rect(0, centerY - 55,  Screen.width, 60), subtitulo, estiloSubtituloGO);

        // Mensaje especial para la pantalla de asustados
        if (!string.IsNullOrEmpty(mensajeExtra))
        {
            GUI.Label(new Rect(0, centerY + 10, Screen.width, 50), mensajeExtra, estiloMensajeGO);
        }

        // ── Botón "Volver a jugar" ───────────────────
        GUIStyle estiloBoton = new GUIStyle(GUI.skin.button);
        estiloBoton.fontSize  = 22;
        estiloBoton.fontStyle = FontStyle.Bold;
        estiloBoton.normal.textColor = Color.white;
        estiloBoton.hover.textColor  = Color.white;
        estiloBoton.active.textColor = Color.white;
        estiloBoton.normal.background = texturaBtnNormal;
        estiloBoton.hover.background  = texturaBtnHover;
        estiloBoton.active.background = texturaBtnActive;

        float btnY = string.IsNullOrEmpty(mensajeExtra) ? centerY + 60 : centerY + 75;
        if (GUI.Button(new Rect(Screen.width / 2f - 140, btnY, 280, 65), "Volver a jugar", estiloBoton))
        {
            ReiniciarJuego();
        }
    }

    // ─────────────────────────────────────────────────
    // UTILIDADES
    // ─────────────────────────────────────────────────
    private Texture2D CrearTexturaColor(Color color)
    {
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, color);
        textura.Apply();
        return textura;
    }
}
