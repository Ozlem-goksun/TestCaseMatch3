using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using DG.Tweening; // DOTween animasyonları için gerekli

public class BoardManager : MonoBehaviour
{

    [Header("Referances")]
    [SerializeField] private LevelManager levelManager;

    [Header("Board Setup")]
    [SerializeField] private GameObject[] tilePrefabs; // Tile prefabları
    [SerializeField] private int width = 5;           // Board genişliği
    [SerializeField] private int height = 5;          // Board yüksekliği

    [SerializeField] private float spacingX = 0.1f;   // Tile'lar arası yatay boşluk
    [SerializeField] private float spacingY = 0.1f;   // Tile'lar arası dikey boşluk

    [Header("Timing")]
    [SerializeField] private float swipeThreshold = 30f;   // Kaydırmayı algılamak için minimum piksel
    [SerializeField] private float slideDuration = 0.2f;   
    [SerializeField] private float clearScaleDuration = 0.2f;
    [SerializeField] private float fallDurationFactor = 0.1f;
    [SerializeField] private float refillSpawnDelay = 0.05f; 
    [SerializeField] private float afterClearDelay = 0.1f;   
    [SerializeField] private float afterFallDelay = 0.1f; 

    // --- İç Değişkenler ---
    private GameObject[,] board;                // Boardtaki Tile'lar için dizi
    private Vector2 startTouch, endTouch;       // Dokunma başlangıç/bitiş pozisyonları
    private bool swipeDetected = false;         // Kaydırma kontrol
    private bool isBoardInteractable = true;    // Oyuncu şu anda hamle yapabilir mi?

    private float tileWidth, tileHeight;
    private float offsetX, offsetY;             // Board'ı ortalamak için ofsetler
    private Camera mainCamera; 

    // DOTWeen
    private Sequence _clearSequence;
    private Sequence _fallSequence;
    private Sequence _refillSequence;


    void Start()
    {
     
        DOTween.Init();

   
        if (levelManager == null) 
        { 
            Debug.LogError("HATA: LevelManager atanmamış!", this.gameObject); 
            enabled = false; return; 
        }

        mainCamera = Camera.main;

        if (mainCamera == null) 
        { 
            Debug.LogError("Main Camera bulunamadı!"); 
            enabled = false; return; 
        }
        if (!ValidateSetup()) 
        { 
            enabled = false;
            return;
        } // Prefablar geçerli mi?

        InitializeBoard();
    }

    void Update()
    {
        if (isBoardInteractable)
        {
            DetectSwipe();
        }
    }

    
    bool ValidateSetup()
    {
        if (tilePrefabs == null || tilePrefabs.Length == 0)
        {
            Debug.LogError("Tile Prefabs atanmamış veya boş!");
            return false;
        }
       

        foreach (var prefab in tilePrefabs)
        {
            if (prefab == null) 
            { 
                Debug.LogError("Tile Prefabs listesinde boş (null) eleman var!"); 
                return false; 
            }
        }
        return true;
    }

    
    void InitializeBoard()
    {
        board = new GameObject[width, height]; // Diziyi oluşturur

        CalculateOffsetsAndSizes(); // Boyutları hesaplar

        // Board'a rasgele Tile atar
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                GameObject newTile = SpawnTileAt(x, y, true); // Başlangıçta eşleşme olmasın
                if (newTile != null)
                {
                    board[x, y] = newTile; // Board'a yerleştir
                }
                else
                {
                    Debug.LogError($"InitializeBoard: Tile spawn edilemedi! Pozisyon: ({x},{y})");
                }
            }
        }

        Debug.Log("Board oluşturuldu. İlk eşleşmeler kontrol ediliyor");

        EnsureNoInitialMatches();
        
        isBoardInteractable = true;

        Debug.Log("Board oynamaya hazır.");
    }

    // Tile boyutunu ve tahtayı ortalamak için gereken ofsetleri hesaplar
    void CalculateOffsetsAndSizes()
    {
        // Boyutları hesaplamak için ilk prefabı kullan
        if (tilePrefabs.Length == 0 || tilePrefabs[0] == null)
        {
            tileWidth = 1f; tileHeight = 1f;
            return;
        }
        SpriteRenderer sr = tilePrefabs[0].GetComponent<SpriteRenderer>();

        if (sr?.sprite == null)
        { // Sprite var mı kontrolü
            tileWidth = 1f; tileHeight = 1f;
        }
        else
        {
            tileWidth = sr.bounds.size.x;
            tileHeight = sr.bounds.size.y;
        }

        // Tahtayı (0,0) merkezine almak için ofsetleri hesapla
        offsetX = (width - 1) * 0.5f * (tileWidth + spacingX);
        offsetY = (height - 1) * 0.5f * (tileHeight + spacingY);
    }

    public void SetInteractable(bool state)
    {
        isBoardInteractable = state;
    }


    public bool IsAnimating()
    {
        return (_clearSequence?.IsActive() ?? false) ||
               (_fallSequence?.IsActive() ?? false) ||
               (_refillSequence?.IsActive() ?? false);
    }

    void ReportMatchesToLevelManager(List<GameObject> matchedTiles)
    {
        if (levelManager == null) return; // LevelManager yoksa çık

        Dictionary<string, int> matchCounts = new Dictionary<string, int>();
        foreach (GameObject tile in matchedTiles)
        {
            if (tile != null && IsValidTag(tile.tag))
            {
                string tag = tile.tag;
                if (matchCounts.ContainsKey(tag))
                {
                    matchCounts[tag]++;
                }
                else
                {
                    matchCounts.Add(tag, 1);
                }
            }
        }

        foreach (var pair in matchCounts)
        {
            levelManager.ReportMatch(pair.Key, pair.Value);
        }
    }


    void DetectSwipe()
    {
        if (Input.GetMouseButtonDown(0))
        {
            startTouch = Input.mousePosition;
            swipeDetected = true;
        }

        if (Input.GetMouseButtonUp(0) && swipeDetected)
        {
            swipeDetected = false;
            endTouch = Input.mousePosition;

            Vector2 delta = endTouch - startTouch; // Kaydırma vektörü

            // Kaydırma mitarı kontrolü
            if (delta.magnitude > swipeThreshold)
            {
                Vector2 direction = GetSwipeDirection(delta); // Kaydırma yönü
                Vector3 worldPoint = mainCamera.ScreenToWorldPoint(startTouch); // Başlangıç noktası

                // Dolu hücreden başlandı mı kontrol et
                if (GetBoardIndexFromWorldPoint(worldPoint, out int startX, out int startY))
                {
                    if (isBoardInteractable)
                    {
                        StartCoroutine(HandleMove(direction, startX, startY));
                    }
                }
            }
        }
    }

    // world Boyutlarını Board boyutlarına uyarla
    bool GetBoardIndexFromWorldPoint(Vector3 worldPoint, out int x, out int y)
    {
        x = -1; y = -1; // Başlangıç değeri
        if (tileWidth <= 0 || tileHeight <= 0) return false; // Geçersiz boyut

        float relativeX = worldPoint.x + offsetX;
        float relativeY = worldPoint.y + offsetY;
        x = Mathf.RoundToInt(relativeX / (tileWidth + spacingX));
        y = Mathf.RoundToInt(relativeY / (tileHeight + spacingY));

        //Board sınırları kontrolü
        return (x >= 0 && x < width && y >= 0 && y < height);
    }

    
    Vector2 GetSwipeDirection(Vector2 delta)
    {
        if (Mathf.Abs(delta.x) > Mathf.Abs(delta.y)) // Yatay hareket daha baskınsa
        {
            return (delta.x > 0) ? Vector2.right : Vector2.left;
        }
        else // Dikey hareket daha baskınsa ya da eşitse
        {
            return (delta.y > 0) ? Vector2.up : Vector2.down;
        }
    }

   
    IEnumerator HandleMove(Vector2 direction, int xIndex, int yIndex)
    {
        isBoardInteractable = false; // Hamle başlarken tahtayı kilitle

        //Taşları kaydır ve animasyonun bitmesini bekle
        yield return StartCoroutine(SlideLineWithAnimation(direction, xIndex, yIndex, slideDuration));

        //Eşleşme ve doldurma döngüsü
        int cascadeCount = 0;
        int maxCascades = 50; // Sonsuz döngü ihtimaline karşı limit

        while (cascadeCount < maxCascades)
        {
            

            List<GameObject> matchedTiles = FindMatches(); // Yeni eşleşmeleri bul

            if (matchedTiles.Count > 0) // Eşleşme varsa...
            {
                cascadeCount++;
                ReportMatchesToLevelManager(matchedTiles); // LevelManager'a aktar

                bool isComplete = levelManager?.IsLevelComplete() ?? false; // Null check ile

                // Eşleşenleri animasyonla yok et
                yield return StartCoroutine(ClearMatchesWithAnimation(matchedTiles));

                // Yok olduktan sonra bir süre bekle
                yield return new WaitForSeconds(afterClearDelay);

                if (isComplete)
                {
                    yield break; // Coroutine'i burada bitir
                }


                // Boşlukları animasyonla doldur
                yield return StartCoroutine(FillBoardWithAnimation());
            }
            else
            {
                // Eşleşme bulunamadı
                break;
            }
        }

        if (cascadeCount >= maxCascades) { Debug.LogError("Maksimum kaskad sayısına ulaşıldı!"); }

        //Tüm işlemler bitti, level bitmediyse tahtayı tekrar etkileşime aç
        if (levelManager != null && !levelManager.IsLevelComplete())
        {
            isBoardInteractable = true;
        }

        yield break; // Coroutine'i bitir
    }

    IEnumerator SlideLineWithAnimation(Vector2 direction, int xIndex, int yIndex, float duration)
    {
        // Mantıksal kaydırma (board dizisini güncelle)
        bool isHorizontal = (direction.y == 0);
        if (isHorizontal)
        {
            GameObject[] row = new GameObject[width];

            for (int x = 0; x < width; x++)
            {
                row[x] = board[x, yIndex];
            }
                

            if (direction == Vector2.right) 
            { 
                GameObject temp = row[width - 1];
                
                for (int x = width - 1; x > 0; x--)
                {
                    row[x] = row[x - 1];
                }
                    
                    row[0] = temp; 
            }
            else 
            {
                GameObject temp = row[0];
                
                for (int x = 0; x < width - 1; x++)
                {
                    row[x] = row[x + 1];
                }
                    
                row[width - 1] = temp; 
            }
            for (int x = 0; x < width; x++)
            {
                board[x, yIndex] = row[x];
            }
        }
        else
        {
            GameObject[] col = new GameObject[height];

            for (int y = 0; y < height; y++)
            {
                col[y] = board[xIndex, y];
            }
                

            if (direction == Vector2.up) 
            { 
                GameObject temp = col[height - 1];

                for (int y = height - 1; y > 0; y--)
                {
                    col[y] = col[y - 1];
                }
                    
                col[0] = temp;
            }

            else 
            { 
                GameObject temp = col[0];
                for (int y = 0; y < height - 1; y++)
                {
                    col[y] = col[y + 1]; col[height - 1] = temp;
                }
                    
            }
            for (int y = 0; y < height; y++) board[xIndex, y] = col[y];
        }

        // Görsel kaydırma (DOTween)
        Sequence slideSequence = DOTween.Sequence().SetId("SlideAnim");
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                if (board[x, y]?.transform != null)
                { // Null check
                    slideSequence.Join(board[x, y].transform.DOMove(GetWorldPosition(x, y), duration).SetEase(Ease.OutCubic));
                }
            }
        }

        // Animasyonun bitmesini bekle
        if (slideSequence.IsActive()) yield return slideSequence.WaitForCompletion();
        slideSequence?.Kill(); // Sekansı temizle
        yield break;
    }

    List<GameObject> FindMatches()
    {
        List<GameObject> matchedTiles = new List<GameObject>();
        // Yatay kontrol
        for (int y = 0; y < height; y++)
        {
            for (int x = 0; x < width - 2;)
            {
                GameObject t1 = board[x, y];

                if (t1 == null || !IsValidTag(t1.tag)) 
                { x++; continue; }

                string currentTag = t1.tag;

                List<GameObject> currentMatch = new List<GameObject>() { t1 };

                int nextX = x + 1;

                while (nextX < width && board[nextX, y]?.CompareTag(currentTag) == true)
                {
                    currentMatch.Add(board[nextX, y]);
                    nextX++;
                }

                if (currentMatch.Count >= 3)
                {
                    foreach (GameObject t in currentMatch)
                        if (!matchedTiles.Contains(t))
                        {
                            matchedTiles.Add(t); 
                        }
                }
                x = nextX; // Kontrol edilenleri atla
            }
        }
        // Dikey kontrol
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height - 2;)
            {
                GameObject t1 = board[x, y];
                if (t1 == null || !IsValidTag(t1.tag)) 
                {
                    y++; continue; 
                }
                string currentTag = t1.tag;

                List<GameObject> currentMatch = new List<GameObject>() { t1 };
                int nextY = y + 1;

                while (nextY < height && board[x, nextY]?.CompareTag(currentTag) == true)
                {
                    currentMatch.Add(board[x, nextY]);
                    nextY++;
                }
                if (currentMatch.Count >= 3)
                { 
                    foreach (GameObject t in currentMatch)
                        if (!matchedTiles.Contains(t))
                        {
                            matchedTiles.Add(t);
                        }
                             
                }
                y = nextY; // Kontrol edilenleri atla
            }
        }
        return matchedTiles;
    }

 
    IEnumerator ClearMatchesWithAnimation(List<GameObject> matchedTiles)
    {
        if (matchedTiles == null || matchedTiles.Count == 0) yield break;

        _clearSequence = DOTween.Sequence().SetId("ClearAnim");
        List<GameObject> tilesToDestroy = new List<GameObject>();

        foreach (GameObject tile in matchedTiles)
        {
            if (tile != null)
            {
                for (int x = 0; x < width; x++)
                {
                    for (int y = 0; y < height; y++)
                    {
                        if (board[x, y] == tile)
                        {
                            board[x, y] = null;
                        }
                            
                    }
                        
                }
                    
               
                _clearSequence.Join(tile.transform.DOScale(Vector3.zero, clearScaleDuration).SetEase(Ease.InBack));
                tilesToDestroy.Add(tile);
            }
        }

        // Animasyonun bitmesini bekle
        if (_clearSequence.IsActive()) yield return _clearSequence.WaitForCompletion();

        // Objeleri yok et
        foreach (GameObject ttd in tilesToDestroy) { if (ttd != null) Destroy(ttd); }

        _clearSequence?.Kill();
        _clearSequence = null;
        yield break;
    }

    IEnumerator FillBoardWithAnimation()
    {
        _fallSequence = DOTween.Sequence().SetId("FallAnim");
        _refillSequence = DOTween.Sequence().SetId("RefillAnim");

        // Düşürme (Collapse)
        for (int x = 0; x < width; x++)
        {
            int emptyY = -1;
            for (int y = 0; y < height; y++)
            {
                if (board[x, y] == null && emptyY == -1) { emptyY = y; }
                else if (board[x, y] != null && emptyY != -1)
                {
                    GameObject fT = board[x, y];
                    board[x, emptyY] = fT; board[x, y] = null;
                    Vector3 tP = GetWorldPosition(x, emptyY); float d = y - emptyY;
                    if (fT?.transform != null) _fallSequence.Join(fT.transform.DOMove(tP, fallDurationFactor * Mathf.Sqrt(d)).SetEase(Ease.OutSine)); // Yumuşak düşüş
                    emptyY++;
                }
            }
        }
        if (_fallSequence.IsActive()) yield return _fallSequence.WaitForCompletion();
        yield return new WaitForSeconds(afterFallDelay);
        _fallSequence?.Kill();
        _fallSequence = null;


        //Doldurma (Refill)
        for (int x = 0; x < width; x++)
        {
            int spawnCount = 0;
            for (int y = height - 1; y >= 0; y--)
            {
                if (board[x, y] == null)
                {
                    Vector3 spawnPos = GetWorldPosition(x, height + spawnCount);
                    GameObject newTile = SpawnTileAt(x, y, true, "", spawnPos); // Spawn et
                    if (newTile != null)
                    {
                        board[x, y] = newTile; // Tahtaya yerleştir
                        Vector3 targetPos = GetWorldPosition(x, y);
                        float distance = (height + spawnCount) - y;
                        if (newTile.transform != null)
                        {
                            _refillSequence.Join(newTile.transform.DOMove(targetPos, fallDurationFactor * Mathf.Sqrt(distance)).SetEase(Ease.OutSine).SetDelay(refillSpawnDelay * spawnCount)); // Animasyonla düşür
                        }
                        spawnCount++;
                    }
                    else { Debug.LogError($"FillBoard: SpawnTileAt null döndürdü ({x},{y})"); } // Spawn hatası
                }
            }
        }
        if (_refillSequence.IsActive())
        {
            yield return _refillSequence.WaitForCompletion();
        }
            
        else yield return null; // Spawn olmadıysa bir frame bekle

        _refillSequence?.Kill();

        _refillSequence = null;

        yield break; // Coroutine bitti
    }

    // Başlangıçta eşleşme olmamasını sağlar
    void EnsureNoInitialMatches()
    {
        int attempts = 0, maxAttempts = 50;

        while (HasInitialMatches() && attempts < maxAttempts)
        {
            attempts++;
            ClearWholeBoard(); // Temizle ve yeniden doldur
            for (int x = 0; x < width; x++)
            {
                for (int y = 0; y < height; y++)
                {
                    GameObject nt = SpawnTileAt(x, y, true);
                    if (nt != null)
                    {
                        board[x, y] = nt;
                    }
                        
                    else
                    {
                        Debug.LogError($"EnsureNoInitialMatches içindeyken SpawnTileAt null ({x},{y})");
                    }
                        
                }
            }
        }
        if (attempts >= maxAttempts)
        {
            Debug.LogError("Başlangıçta eşleşme olabilir.");
        }
            
    }

    // Board'daki Tile'ları yok eder
    void ClearWholeBoard() 
    { 
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            { 
                if (board[x, y] != null)
                { 
                    Destroy(board[x, y]);
                    board[x, y] = null; 
                } 
            } 
        }
    }

    bool HasInitialMatches()
    {
        for (int x = 0; x < width; x++)
        { 
            for (int y = 0; y < height; y++) 
            {
                GameObject cT = board[x, y];
                if (cT == null)
                {
                    continue; string cTag = cT.tag;
                }
                if (!IsValidTag(cTag))
                {
                    continue;
                }
                if (x < width - 2 && board[x + 1, y]?.CompareTag(cTag) == true && board[x + 2, y]?.CompareTag(cTag) == true)
                {
                    return true;
                }
                if (y < height - 2 && board[x, y + 1]?.CompareTag(cTag) == true && board[x, y + 2]?.CompareTag(cTag) == true)
                {
                    return true;
                }
                     
            }
        }
        return false; // Eşleşme yok
    }

    //Tile Spawn Etme
    GameObject SpawnTileAt(int gridX, int gridY, bool ensureNoMatch, string excludeTag = "", Vector3? initialPosition = null)
    {
        // Geçerli Prefablar
        List<int> possibleIndices = new List<int>();

        for (int i = 0; i < tilePrefabs.Length; i++)
        {
            if (tilePrefabs[i] != null && IsValidTag(tilePrefabs[i].tag))
            {
                if (string.IsNullOrEmpty(excludeTag) || !tilePrefabs[i].CompareTag(excludeTag))
                {
                    possibleIndices.Add(i);
                }
            }
        }

        if (possibleIndices.Count == 0)
        { 
            return null; 
        }

        List<int> availableIndices = new List<int>();
        if (ensureNoMatch)
        {
            foreach (int index in possibleIndices)
            {
                string currentPrefabTag = tilePrefabs[index].tag;
                bool createsMatch = false;

                //SOL ve AŞAĞI kontrolü
                if (gridX >= 2 && board[gridX - 1, gridY]?.CompareTag(currentPrefabTag) == true && board[gridX - 2, gridY]?.CompareTag(currentPrefabTag) == true)
                {
                    createsMatch = true;
                }
                    
                if (!createsMatch && gridY >= 2 && board[gridX, gridY - 1]?.CompareTag(currentPrefabTag) == true && board[gridX, gridY - 2]?.CompareTag(currentPrefabTag) == true)
                {
                    createsMatch = true;
                }
                if (!createsMatch)
                {
                    availableIndices.Add(index);
                }
                    
            }
        }
        else
        {
            availableIndices.AddRange(possibleIndices);
        }

        // Son prefab indexini seç
        int finalIndex;

        if (availableIndices.Count == 0)
        {
            finalIndex = possibleIndices[Random.Range(0, possibleIndices.Count)];
        }
        else
        { // Uygun olanlardan rastgele seç
            finalIndex = availableIndices[Random.Range(0, availableIndices.Count)];
        }

        // Spawn et
        GameObject prefabToSpawn = tilePrefabs[finalIndex];

        Vector3 spawnPos = initialPosition ?? GetWorldPosition(gridX, gridY); // Başlangıç pozisyonunu kullan

        GameObject tileObject = Instantiate(prefabToSpawn, spawnPos, Quaternion.identity, this.transform); // Parent'ı ayarla
        
        return tileObject; // Oluşturulan objeyi döndür
    }

    // World koordinatını hesaplar
    Vector3 GetWorldPosition(int x, int y)
    {
        float pX = transform.position.x + (x * (tileWidth + spacingX)) - offsetX;
        float pY = transform.position.y + (y * (tileHeight + spacingY)) - offsetY;
        return new Vector3(pX, pY, 0);
    }

    // Tag'in geçerli olup olmadığını kontrol eder
    bool IsValidTag(string tag)
    {
        return !string.IsNullOrEmpty(tag) && tag != "Untagged";
    }
}