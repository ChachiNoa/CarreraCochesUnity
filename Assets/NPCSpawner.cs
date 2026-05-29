using UnityEngine;
using System.Collections;
using System.Collections.Generic;

/// <summary>
/// Genera NPCs aleatoriamente en una acera y los hace cruzar la carretera hacia la acera opuesta.
/// Detecta automáticamente los personajes completos del mapa (los que tienen "character" en su nombre)
/// y los usa como plantilla para crear clones.
/// Soporta generación dinámica que sigue al coche por todo el circuito.
/// </summary>
public class NPCSpawner : MonoBehaviour
{
    [Header("Plantillas de NPCs")]
    [Tooltip("Arrastra aquí los prefabs de los NPCs. Si lo dejas vacío, el script buscará automáticamente los personajes completos del mapa.")]
    public GameObject[] npcPrefabs;

    [Header("Ajustes de Generación Dinámica (Seguir al Coche)")]
    [Tooltip("Si está activo, los peatones aparecerán delante del coche por todo el circuito.")]
    public bool seguirAlCoche = true;
    [Tooltip("Distancia mínima por delante del coche a la que aparecerán los peatones.")]
    public float distanciaMinimaDelante = 25f;
    [Tooltip("Distancia máxima por delante del coche a la que aparecerán los peatones.")]
    public float distanciaMaximaDelante = 45f;
    [Tooltip("Ancho de la carretera (distancia desde el centro hasta la acera).")]
    public float semiAnchoCarretera = 4.5f;

    [Header("Ajustes de Coordenadas Fijas (Fallback)")]
    [Tooltip("Coordenada X de la acera IZQUIERDA.")]
    public float aceraIzquierdaX = -6.5f;
    [Tooltip("Coordenada X de la acera DERECHA.")]
    public float aceraDerechaX = 2.2f;
    [Tooltip("Coordenada Z mínima.")]
    public float rangoZMin = 15f;
    [Tooltip("Coordenada Z máxima.")]
    public float rangoZMax = 65f;

    [Header("Puntos de Control Manuales (Opcional)")]
    [Tooltip("Si asignas estos objetos, se ignorará todo lo demás y se usarán estos puntos.")]
    public Transform aceraOrigenInicio;
    public Transform aceraOrigenFin;
    public Transform aceraDestinoInicio;
    public Transform aceraDestinoFin;

    [Header("Ajustes de Generación (Spawn)")]
    [Tooltip("Tiempo mínimo en segundos entre la aparición de cada NPC.")]
    public float intervaloMinimo = 3.0f;
    [Tooltip("Tiempo máximo en segundos entre la aparición de cada NPC.")]
    public float intervaloMaximo = 7.0f;

    [Header("Ajustes de los Peatones")]
    [Tooltip("Velocidad de movimiento de los NPCs al cruzar la carretera.")]
    public float velocidadCaminarNPC = 1.8f;

    [Tooltip("Altura Y por defecto (usada si fallan las físicas).")]
    public float alturaSpawnDefault = 1.0f;

    void Start()
    {
        // --- 0. ASEGURAR QUE EL GAME MANAGER ESTÉ INSTALADO EN LA ESCENA ---
        NPCGameManager gameManager = FindObjectOfType<NPCGameManager>();
        if (gameManager == null)
        {
            gameObject.AddComponent<NPCGameManager>();
        }

        // --- 1. BUSCAR PERSONAJES COMPLETOS EN LA ESCENA ---
        if (npcPrefabs == null || npcPrefabs.Length == 0)
        {
            BuscarPlantillasEnEscena();
        }

        // --- 2. EMPEZAR A GENERAR PEATONES ---
        StartCoroutine(BucleGeneracionNPCs());
    }

    /// <summary>
    /// Busca SOLO personajes NPC completos en la escena (los que tienen "character" en su nombre).
    /// </summary>
    private void BuscarPlantillasEnEscena()
    {
        GameObject[] todosLosObjetos = FindObjectsOfType<GameObject>();
        List<GameObject> personajesEncontrados = new List<GameObject>();

        foreach (GameObject obj in todosLosObjetos)
        {
            if (SimpleNPCWalker.EsNPCCompleto(obj))
            {
                personajesEncontrados.Add(obj);
                Debug.Log($"<color=cyan><b>[NPC Spawner]</b> Personaje completo encontrado: '{obj.name}' en posición {obj.transform.position}</color>");
            }
        }

        if (personajesEncontrados.Count > 0)
        {
            npcPrefabs = personajesEncontrados.ToArray();

            // Desactivamos los originales para que no se queden parados
            foreach (GameObject npc in npcPrefabs)
            {
                npc.SetActive(false);
            }

            Debug.Log($"<color=green><b>[NPC Spawner]</b> ¡Se encontraron {npcPrefabs.Length} personajes completos! Se ocultaron los originales.</color>");
        }
        else
        {
            Debug.LogError("<b>[NPC Spawner]</b> ¡No se encontró ningún personaje NPC con 'character' en su nombre!");
        }
    }

    private IEnumerator BucleGeneracionNPCs()
    {
        yield return new WaitForSeconds(1.0f);

        while (true)
        {
            if (npcPrefabs != null && npcPrefabs.Length > 0)
            {
                SpawnNPC();
            }

            float tiempoEspera = Random.Range(intervaloMinimo, intervaloMaximo);
            yield return new WaitForSeconds(tiempoEspera);
        }
    }

    private void SpawnNPC()
    {
        GameObject plantillaElegida = npcPrefabs[Random.Range(0, npcPrefabs.Length)];
        if (plantillaElegida == null) return;

        Vector3 posicionSpawn = Vector3.zero;
        Vector3 posicionDestino = Vector3.zero;
        bool exitoSpawn = false;

        // 1. INTENTAR GENERACIÓN DINÁMICA DELANTE DEL COCHE
        if (seguirAlCoche)
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

            if (transformCoche != null)
            {
                float distanciaAdelante = Random.Range(distanciaMinimaDelante, distanciaMaximaDelante);
                
                // Dirección hacia adelante del coche en el plano horizontal
                Vector3 dirForward = transformCoche.forward;
                dirForward.y = 0f;
                dirForward.Normalize();

                // Dirección derecha perpendicular
                Vector3 dirRight = Vector3.Cross(Vector3.up, dirForward).normalized;

                // Centro de la carretera adelante
                Vector3 centroCarreteraDelante = transformCoche.position + dirForward * distanciaAdelante;

                // Decidimos acera de inicio
                bool empiezaEnIzquierda = Random.value > 0.5f;

                if (empiezaEnIzquierda)
                {
                    posicionSpawn = centroCarreteraDelante - dirRight * semiAnchoCarretera;
                    posicionDestino = centroCarreteraDelante + dirRight * semiAnchoCarretera;
                }
                else
                {
                    posicionSpawn = centroCarreteraDelante + dirRight * semiAnchoCarretera;
                    posicionDestino = centroCarreteraDelante - dirRight * semiAnchoCarretera;
                }

                // Ajustamos la altura de forma inteligente usando Raycast para detectar el suelo
                posicionSpawn.y = ObtenerAlturaSuelo(posicionSpawn, transformCoche.position.y);
                posicionDestino.y = ObtenerAlturaSuelo(posicionDestino, transformCoche.position.y);
                
                exitoSpawn = true;
            }
        }

        // 2. FALLBACK A PUNTOS MANUALES
        if (!exitoSpawn && aceraOrigenInicio != null && aceraOrigenFin != null && aceraDestinoInicio != null && aceraDestinoFin != null)
        {
            float factorAleatorio = Random.value;
            posicionSpawn = Vector3.Lerp(aceraOrigenInicio.position, aceraOrigenFin.position, factorAleatorio);
            posicionSpawn.y = alturaSpawnDefault;

            posicionDestino = Vector3.Lerp(aceraDestinoInicio.position, aceraDestinoFin.position, factorAleatorio);
            posicionDestino.y = alturaSpawnDefault;
            exitoSpawn = true;
        }

        // 3. FALLBACK A COORDENADAS FIJAS
        if (!exitoSpawn)
        {
            float randomZ = Random.Range(rangoZMin, rangoZMax);
            bool empiezaEnIzquierda = Random.value > 0.5f;

            if (empiezaEnIzquierda)
            {
                posicionSpawn = new Vector3(aceraIzquierdaX, alturaSpawnDefault, randomZ);
                posicionDestino = new Vector3(aceraDerechaX, alturaSpawnDefault, randomZ);
            }
            else
            {
                posicionSpawn = new Vector3(aceraDerechaX, alturaSpawnDefault, randomZ);
                posicionDestino = new Vector3(aceraIzquierdaX, alturaSpawnDefault, randomZ);
            }
        }

        // Clonamos el NPC
        GameObject nuevoNPC = Instantiate(plantillaElegida, posicionSpawn, Quaternion.identity);
        nuevoNPC.transform.parent = transform;
        nuevoNPC.name = "NPC_Peaton_" + Random.Range(100, 999);
        nuevoNPC.SetActive(true);

        // Aseguramos script de caminar
        SimpleNPCWalker walker = nuevoNPC.GetComponent<SimpleNPCWalker>();
        if (walker == null)
        {
            walker = nuevoNPC.AddComponent<SimpleNPCWalker>();
        }

        walker.velocidadAvance = velocidadCaminarNPC;
        walker.SetTarget(posicionDestino);

        string direccionTexto = (posicionSpawn.x < posicionDestino.x) ? "Izquierda -> Derecha" : "Derecha -> Izquierda";
        Debug.Log($"<color=lime><b>[NPC Spawner]</b> ¡Peatón '{nuevoNPC.name}' generado! Sentido: {direccionTexto} | Pos: {posicionSpawn} -> Destino: {posicionDestino}</color>");
    }

    /// <summary>
    /// Detecta la altura exacta del suelo físico en una coordenada usando Raycast, 
    /// ignorando triggers invisibles y añadiendo un pequeño margen de seguridad para evitar que floten o se hundan.
    /// </summary>
    private float ObtenerAlturaSuelo(Vector3 posicion, float alturaReferenciaCoche)
    {
        // Lanzamos un rayo desde 20 metros arriba de la referencia del coche hacia abajo para cubrir desniveles grandes (puentes, rampas)
        Vector3 origenRayo = new Vector3(posicion.x, alturaReferenciaCoche + 20f, posicion.z);
        RaycastHit hit;

        // IMPORTANTE: Ignoramos los triggers (QueryTriggerInteraction.Ignore) como checkpoints, 
        // zonas de coleccionables o triggers de vueltas para evitar spawnear flotando.
        if (Physics.Raycast(origenRayo, Vector3.down, out hit, 40f, Physics.DefaultRaycastLayers, QueryTriggerInteraction.Ignore))
        {
            // Añadimos un pequeño margen de 0.05f (5 centímetros) por encima del punto exacto de colisión
            // para asegurar que las extremidades y pies del peatón queden perfectamente por encima del mesh de la acera.
            return hit.point.y + 0.05f;
        }

        // Fallback si no choca con ningún collider físico (por ejemplo, al salirse del mapa)
        return alturaReferenciaCoche + 0.05f;
    }
}
