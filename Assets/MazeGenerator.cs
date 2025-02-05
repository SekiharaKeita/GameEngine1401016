using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MazeQLearningAI : MonoBehaviour
{
    public int mazeSize = 5;  // 初期迷路サイズ（5×5）
    public float learningRate = 0.1f;       // 学習率(0.1(10%)は新しい情報、残りの0.9(90%)はこれまでの情報を反映する)
    public float discountFactor = 0.9f;     // 割引率(将来の報酬をどの程度重視するか。1に近いと将来的な報酬を重視し、長期的な利益を追求する)
    public float explorationRate = 1.0f;    // 探索率(1.0の場合完全にランダム。0の場合常に現在の最良の選択肢を選ぶ)
    public float explorationDecay = 0.95f; // 探索率の減衰(explorationRateの値の減衰)

    private int[,] maze;
    private float[,,] qTable;
    private Vector2Int startPos;
    private Vector2Int goalPos;
    private Vector2Int aiPos;
    private GameObject aiAgent;

    private enum Action { Up, Down, Left, Right }
    private int[] dx = { 0, 0, -1, 1 };
    private int[] dy = { -1, 1, 0, 0 };

    public GameObject wallPrefab;
    public GameObject floorPrefab;
    public GameObject startPrefab;
    public GameObject goalPrefab;
    public GameObject aiPrefab;

    void Start()
    {
        StartCoroutine(GameLoop());
    }

    IEnumerator GameLoop()
    {
        while (true)
        {
            GenerateMaze();
            if (qTable == null || qTable.GetLength(0) != mazeSize)
            {
                qTable = new float[mazeSize, mazeSize, 4];
            }


            aiAgent = Instantiate(aiPrefab, new Vector3(startPos.x, 0.5f, startPos.y), Quaternion.identity);

            yield return StartCoroutine(TrainAI());

            Destroy(aiAgent);

            // 迷路サイズを増やし、新しい迷路を作成
            mazeSize += 2;
            Debug.Log($"ゴール達成！ 新しい迷路サイズ: {mazeSize}×{mazeSize}");

            yield return new WaitForSeconds(1.0f); // 迷路のリセットが見えるようにする

            GenerateMaze(); // ここで新しい迷路を生成
        }
    }

    void GenerateMaze()
    {
        // ?? 以前の迷路のオブジェクトをすべて削除（スタートを除外）
        foreach (Transform child in transform)
        {
            if (child.gameObject.tag != "Start") // スタートオブジェクトは削除しない
            {
                Destroy(child.gameObject);
            }
        }

        maze = new int[mazeSize, mazeSize];

        for (int x = 0; x < mazeSize; x++)
        {
            for (int y = 0; y < mazeSize; y++)
            {
                maze[x, y] = 1;
            }
        }

        startPos = new Vector2Int(1, 1);
        goalPos = new Vector2Int(mazeSize - 2, mazeSize - 2);
        aiPos = startPos;

        maze[startPos.x, startPos.y] = 0;
        DFS(startPos.x, startPos.y);

        DrawMaze();
        SetupCamera();

        // 以前のゴールをすべて削除（1つではなく複数削除）
        GameObject[] oldGoals = GameObject.FindGameObjectsWithTag("Goal");
        foreach (GameObject oldGoal in oldGoals)
        {
            Destroy(oldGoal);
        }

        // 以前のスタートが消えている可能性があるため、スタートを再生成
        if (GameObject.FindGameObjectWithTag("Start") == null)
        {
            GameObject newStart = Instantiate(startPrefab, new Vector3(startPos.x, 0.5f, startPos.y), Quaternion.identity);
            newStart.tag = "Start";
        }

        // 新しいゴールを生成し、タグを設定
        GameObject newGoal = Instantiate(goalPrefab, new Vector3(goalPos.x, 0.5f, goalPos.y), Quaternion.identity);
        newGoal.tag = "Goal";

        // 迷路が作成された後、距離マップを計算
        CalculateDistanceMap();
    }


    private int[,] distanceMap; // 各セルのゴールまでの最短距離を保存するマップ

    void CalculateDistanceMap()
    {
        int width = mazeSize;
        int height = mazeSize;
        distanceMap = new int[width, height];

        // 初期化：すべてのセルを「未探索」にする
        for (int x = 0; x < width; x++)
        {
            for (int y = 0; y < height; y++)
            {
                distanceMap[x, y] = int.MaxValue; // 初期値として非常に大きな数を設定
            }
        }

        Queue<Vector2Int> queue = new Queue<Vector2Int>();
        queue.Enqueue(goalPos);
        distanceMap[goalPos.x, goalPos.y] = 0; // ゴール地点の距離は 0

        int[] dx = { 1, -1, 0, 0 };
        int[] dy = { 0, 0, 1, -1 };

        while (queue.Count > 0)
        {
            Vector2Int current = queue.Dequeue();
            int currentDistance = distanceMap[current.x, current.y];

            for (int i = 0; i < 4; i++)
            {
                int nx = current.x + dx[i];
                int ny = current.y + dy[i];

                if (nx >= 0 && ny >= 0 && nx < width && ny < height && maze[nx, ny] == 0) // 通れる道か確認
                {
                    if (distanceMap[nx, ny] > currentDistance + 1) // より短い距離が見つかったら更新
                    {
                        distanceMap[nx, ny] = currentDistance + 1;
                        queue.Enqueue(new Vector2Int(nx, ny));
                    }
                }
            }
        }
    }


    void DFS(int x, int y)
    {
        int[][] directions = { new int[] { 0, -2 }, new int[] { 2, 0 }, new int[] { 0, 2 }, new int[] { -2, 0 } };
        Shuffle(directions);

        foreach (var dir in directions)
        {
            int nx = x + dir[0];
            int ny = y + dir[1];

            if (nx > 0 && ny > 0 && nx < mazeSize - 1 && ny < mazeSize - 1 && maze[nx, ny] == 1)
            {
                maze[nx, ny] = 0;
                maze[x + dir[0] / 2, y + dir[1] / 2] = 0;
                DFS(nx, ny);
            }
        }
    }

    void Shuffle(int[][] array)
    {
        for (int i = array.Length - 1; i > 0; i--)
        {
            int j = Random.Range(0, i + 1);
            var temp = array[i];
            array[i] = array[j];
            array[j] = temp;
        }
    }

    void DrawMaze()
    {
        foreach (Transform child in transform)
        {
            Destroy(child.gameObject);
        }

        for (int x = 0; x < mazeSize; x++)
        {
            for (int y = 0; y < mazeSize; y++)
            {
                Vector3 pos = new Vector3(x, 0, y);
                if (maze[x, y] == 1)
                {
                    Instantiate(wallPrefab, pos, Quaternion.identity, transform);
                }
                else
                {
                    Instantiate(floorPrefab, pos, Quaternion.identity, transform);
                }
            }
        }
    }



    IEnumerator TrainAI()
    {
        Dictionary<Vector2Int, int> visitedCells = new Dictionary<Vector2Int, int>();

        for (int episode = 0; episode < 100; episode++)
        {
            aiPos = startPos;
            aiAgent.transform.position = new Vector3(aiPos.x, 0.5f, aiPos.y);
            visitedCells.Clear();

            while (aiPos != goalPos)
            {
                Action action = ChooseAction(aiPos);
                Vector2Int nextPos = aiPos + new Vector2Int(dx[(int)action], dy[(int)action]);

                if (maze[nextPos.x, nextPos.y] == 1) // 壁に衝突
                {
                    continue;
                }

                if (!visitedCells.ContainsKey(nextPos))
                    visitedCells[nextPos] = 0;
                visitedCells[nextPos]++;

                float revisitPenalty = visitedCells[nextPos] * -1.0f;

                // 修正: ゴールまでの距離を「順路」に基づいて計算
                float goalDistanceBefore = distanceMap[aiPos.x, aiPos.y];
                float goalDistanceAfter = distanceMap[nextPos.x, nextPos.y];
                float distanceReward = (goalDistanceBefore - goalDistanceAfter) * 5.0f;

                float reward = -1 + distanceReward + revisitPenalty;

                if (nextPos == goalPos)
                    reward = 150;

                float oldQ = qTable[aiPos.x, aiPos.y, (int)action];
                float maxNextQ = Mathf.Max(qTable[nextPos.x, nextPos.y, 0],
                                           qTable[nextPos.x, nextPos.y, 1],
                                           qTable[nextPos.x, nextPos.y, 2],
                                           qTable[nextPos.x, nextPos.y, 3]);

                qTable[aiPos.x, aiPos.y, (int)action] = oldQ + learningRate * (reward + discountFactor * maxNextQ - oldQ);

             // デバッグログ
             // Debug.Log($"Q更新: {aiPos} -> {nextPos} | Action: {action} | Old Q: {oldQ} | New Q: {qTable[aiPos.x, aiPos.y, (int)action]}");
             // Debug.Log($"エピソード {episode}: 探索率 {explorationRate}");

                aiPos = nextPos;
                aiAgent.transform.position = new Vector3(aiPos.x, 0.5f, aiPos.y);

                yield return new WaitForSeconds(0.02f);
            }

            explorationRate *= explorationDecay;
            Debug.Log("AIがゴールしました！");
            yield break;
        }
    }


    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float cameraHeight = mazeSize * 1.2f; // 迷路サイズに合わせて高さを変更
            mainCamera.transform.position = new Vector3(mazeSize / 2f, cameraHeight, mazeSize / 2f);
            mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            mainCamera.orthographic = true; // 俯瞰（2D視点）にする
            mainCamera.orthographicSize = mazeSize / 2f; // 迷路全体を映す
        }
    }


    Action ChooseAction(Vector2Int state)
    {
        if (Random.value < explorationRate)
        {
            return (Action)Random.Range(0, 4);
        }
        else
        {
            int bestAction = 0;
            float maxQValue = qTable[state.x, state.y, 0];

            for (int i = 1; i < 4; i++)
            {
                if (qTable[state.x, state.y, i] > maxQValue)
                {
                    maxQValue = qTable[state.x, state.y, i];
                    bestAction = i;
                }
            }

            return (Action)bestAction;
        }
    }
}
