using UnityEngine;
using UnityEngine.SceneManagement;

/// <summary>
/// Gestiona la puntuación del juego y las pantallas cómicas de fin de partida.
/// Utiliza OnGUI para garantizar un renderizado 100% infalible en cualquier versión de Unity.
/// Soporta el sonido de la bocina al mantener Espacio y asustar a los peatones cercanos.
/// </summary>
public class NPCGameManager : MonoBehaviour
{
    public static NPCGameManager Instance { get; private set; }

    [Header("Ajustes del Juego")]
    [Tooltip("Límite de peatones para ganar o perder la partida.")]
    public int limiteObjetivo = 10;

    [Header("Ajustes de Bocina (Espacio)")]
    [Tooltip("Sonido de la bocina que sonará en bucle al mantener pulsada la barra espaciadora.")]
    public AudioClip sonidoClaxon;
    [Tooltip("Distancia máxima a la que la bocina asustará a los peatones.")]
    public float rangoBocina = 12.0f;

    private int peatonesAtropellados = 0;
    private int peatonesVivos = 0;
    private int peatonesAsustados = 0;
    private bool juegoTerminado = false;

    // Componente para reproducir la bocina en bucle
    private AudioSource claxonAudioSource;

    // Caché de texturas para evitar recrearlas en cada frame
    private Texture2D texturaHUD;
    private Texture2D texturaFondoGO;
    private Texture2D texturaBtnNormal;
    private Texture2D texturaBtnHover;
    private Texture2D texturaBtnActive;

    void Awake()
    {
        // Implementación de Singleton
        if (Instance == null)
        {
            Instance = this;
        }
        else
        {
            Destroy(gameObject);
            return;
        }

        // Restablecer el tiempo de juego al iniciar
        Time.timeScale = 1f;

        // Inicializamos las texturas premium para nuestra UI
        texturaHUD = CrearTexturaColor(new Color(0.07f, 0.07f, 0.07f, 0.85f));
        texturaFondoGO = CrearTexturaColor(new Color(0.04f, 0.04f, 0.04f, 0.95f));
        texturaBtnNormal = CrearTexturaColor(new Color(0.12f, 0.45f, 0.9f));
        texturaBtnHover = CrearTexturaColor(new Color(0.2f, 0.55f, 1f));
        texturaBtnActive = CrearTexturaColor(new Color(0.08f, 0.35f, 0.75f));

        // Configuramos el AudioSource para el claxon en bucle de forma dinámica
        claxonAudioSource = gameObject.AddComponent<AudioSource>();
        claxonAudioSource.playOnAwake = false;
        claxonAudioSource.loop = true;
    }

    void Update()
    {
        if (juegoTerminado)
        {
            if (claxonAudioSource.isPlaying) claxonAudioSource.Stop();
            return;
        }

        // Asignamos el clip si ha cambiado en el Inspector
        if (claxonAudioSource.clip != sonidoClaxon)
        {
            claxonAudioSource.clip = sonidoClaxon;
        }

        // --- CONTROL DEL CLAXON / BOCINA (MANTENER ESPACIO) ---
        if (Input.GetKeyDown(KeyCode.Space))
        {
            if (sonidoClaxon != null && !claxonAudioSource.isPlaying)
            {
                claxonAudioSource.Play();
            }
        }

        if (Input.GetKey(KeyCode.Space))
        {
            // Si el claxon está sonando, asustar peatones en un radio determinado desde el coche
            ScannearYAsustarPeatones();
        }

        if (Input.GetKeyUp(KeyCode.Space))
        {
            if (claxonAudioSource.isPlaying)
            {
                claxonAudioSource.Stop();
            }
        }
    }

    /// <summary>
    /// Escanea todos los peatones en escena y asusta a los que estén cerca del coche.
    /// </summary>
    private void ScannearYAsustarPeatones()
    {
        // Encontramos el coche en la escena
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

        // Buscamos todos los SimpleNPCWalker activos
        SimpleNPCWalker[] peatones = FindObjectsOfType<SimpleNPCWalker>();
        foreach (SimpleNPCWalker peaton in peatones)
        {
            if (Vector3.Distance(transformCoche.position, peaton.transform.position) <= rangoBocina)
            {
                // Asustamos al peatón
                peaton.Asustar();
            }
        }
    }

    /// <summary>
    /// Registra cuando un peatón ha sido golpeado por el coche.
    /// </summary>
    public void RegistrarPeatonAtropellado()
    {
        if (juegoTerminado) return;

        peatonesAtropellados++;

        if (peatonesAtropellados >= limiteObjetivo)
        {
            TerminarJuego();
        }
    }

    /// <summary>
    /// Registra cuando un peatón cruza con éxito la carretera sin ser atropellado o asustado.
    /// </summary>
    public void RegistrarPeatonCruzado()
    {
        if (juegoTerminado) return;

        peatonesVivos++;

        if (peatonesVivos >= limiteObjetivo)
        {
            TerminarJuego();
        }
    }

    /// <summary>
    /// Registra cuando un peatón es asustado por el claxon.
    /// </summary>
    public void RegistrarPeatonAsustado()
    {
        if (juegoTerminado) return;

        peatonesAsustados++;

        if (peatonesAsustados >= limiteObjetivo)
        {
            TerminarJuego();
        }
    }

    private void TerminarJuego()
    {
        juegoTerminado = true;
        // Pausar el juego física y temporalmente
        Time.timeScale = 0f;
    }

    private void ReiniciarJuego()
    {
        Time.timeScale = 1f;
        SceneManager.LoadScene(SceneManager.GetActiveScene().name);
    }

    /// <summary>
    /// Renderizado directo e infalible de la interfaz gráfica en pantalla.
    /// </summary>
    void OnGUI()
    {
        // ==========================================
        // 1. RENDERIZADO DEL HUD (MARCADORES)
        // ==========================================
        
        // Estilo del fondo del HUD (caja oscura translúcida)
        GUIStyle estiloHUD = new GUIStyle(GUI.skin.box);
        estiloHUD.normal.background = texturaHUD;
        
        // Dibujar contenedor en la esquina superior izquierda (más alto para albergar 3 contadores)
        GUI.Box(new Rect(20, 20, 310, 110), "", estiloHUD);

        // Estilo del texto de Atropellados (Rojo vibrante cómico)
        GUIStyle estiloAtropellados = new GUIStyle(GUI.skin.label);
        estiloAtropellados.fontSize = 16;
        estiloAtropellados.fontStyle = FontStyle.Bold;
        estiloAtropellados.normal.textColor = new Color(1f, 0.3f, 0.3f);
        
        // Estilo del texto de Vivos (Verde brillante)
        GUIStyle estiloVivos = new GUIStyle(GUI.skin.label);
        estiloVivos.fontSize = 16;
        estiloVivos.fontStyle = FontStyle.Bold;
        estiloVivos.normal.textColor = new Color(0.3f, 1f, 0.3f);

        // Estilo del texto de Asustados (Naranja/Amarillo vibrante)
        GUIStyle estiloAsustados = new GUIStyle(GUI.skin.label);
        estiloAsustados.fontSize = 16;
        estiloAsustados.fontStyle = FontStyle.Bold;
        estiloAsustados.normal.textColor = new Color(1f, 0.65f, 0.15f);

        // Dibujar textos del HUD limpios de emojis y más pequeños
        GUI.Label(new Rect(35, 27, 280, 25), $"Personas atropelladas: {peatonesAtropellados} / {limiteObjetivo}", estiloAtropellados);
        GUI.Label(new Rect(35, 55, 280, 25), $"Personas vivas: {peatonesVivos} / {limiteObjetivo}", estiloVivos);
        GUI.Label(new Rect(35, 83, 280, 25), $"Personas asustadas: {peatonesAsustados} / {limiteObjetivo}", estiloAsustados);

        // ==========================================
        // 2. RENDERIZADO DE PANTALLA DE GAME OVER
        // ==========================================
        if (juegoTerminado)
        {
            // Panel de fondo completo oscuro translúcido
            GUIStyle estiloFondoGO = new GUIStyle(GUI.skin.box);
            estiloFondoGO.normal.background = texturaFondoGO;
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "", estiloFondoGO);

            string titulo = "";
            Color colorTitulo = Color.white;
            string subtitulo = "";

            if (peatonesAtropellados >= limiteObjetivo)
            {
                titulo = "¡ERES UN BUEN CONDUCTOR!";
                colorTitulo = new Color(0.3f, 1f, 0.3f); // Verde
                subtitulo = "¡Lograste atropellar a 10 peatones! Eres el rey absoluto del asfalto.";
            }
            else if (peatonesAsustados >= limiteObjetivo)
            {
                titulo = "¡CONDUCES DE FORMA PECULIAR!";
                colorTitulo = new Color(1f, 0.65f, 0.15f); // Naranja/Amarillo
                subtitulo = "¡Y se nota que te gustan mucho los coches de choque!";
            }
            else
            {
                titulo = "¡ERES UN MAL CONDUCTOR!";
                colorTitulo = new Color(1f, 0.3f, 0.3f); // Rojo
                subtitulo = "Dejaste cruzar a 10 peatones sanos y salvos... ¿Dónde quedó tu furia al volante?";
            }

            // Estilo del Título de Game Over
            GUIStyle estiloTituloGO = new GUIStyle(GUI.skin.label);
            estiloTituloGO.fontSize = 46;
            estiloTituloGO.fontStyle = FontStyle.Bold;
            estiloTituloGO.alignment = TextAnchor.MiddleCenter;
            estiloTituloGO.normal.textColor = colorTitulo;

            // Estilo del Subtítulo descriptivo
            GUIStyle estiloSubtituloGO = new GUIStyle(GUI.skin.label);
            estiloSubtituloGO.fontSize = 22;
            estiloSubtituloGO.alignment = TextAnchor.MiddleCenter;
            estiloSubtituloGO.normal.textColor = new Color(0.85f, 0.85f, 0.85f);

            // Posiciones en el centro de la pantalla
            float centerY = Screen.height / 2f;
            
            GUI.Label(new Rect(0, centerY - 140, Screen.width, 70), titulo, estiloTituloGO);
            GUI.Label(new Rect(0, centerY - 50, Screen.width, 60), subtitulo, estiloSubtituloGO);

            // Estilo del botón "Volver a jugar" (Premium, azul vibrante interactivo)
            GUIStyle estiloBoton = new GUIStyle(GUI.skin.button);
            estiloBoton.fontSize = 22;
            estiloBoton.fontStyle = FontStyle.Bold;
            estiloBoton.normal.textColor = Color.white;
            estiloBoton.hover.textColor = Color.white;
            estiloBoton.active.textColor = Color.white;
            
            // Asignamos las texturas dinámicas para los estados del botón
            estiloBoton.normal.background = texturaBtnNormal;
            estiloBoton.hover.background = texturaBtnHover;
            estiloBoton.active.background = texturaBtnActive;

            // Dibujar y capturar clic del botón
            if (GUI.Button(new Rect(Screen.width / 2f - 140, centerY + 50, 280, 65), "Volver a jugar", estiloBoton))
            {
                ReiniciarJuego();
            }
        }
    }

    /// <summary>
    /// Genera dinámicamente texturas de color plano en tiempo de ejecución.
    /// </summary>
    private Texture2D CrearTexturaColor(Color color)
    {
        Texture2D textura = new Texture2D(1, 1);
        textura.SetPixel(0, 0, color);
        textura.Apply();
        return textura;
    }
}
