using UnityEngine;
using System.Collections;
using System.Collections.Generic;
using System.Linq;

public class MazeQLearningAI : MonoBehaviour
{
    public int mazeSize = 5;  // �������H�T�C�Y�i5�~5�j
    public float learningRate = 0.1f;
    public float discountFactor = 0.9f;
    public float explorationRate = 1.0f;
    public float explorationDecay = 0.995f;

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
            qTable = new float[mazeSize, mazeSize, 4];

            aiAgent = Instantiate(aiPrefab, new Vector3(startPos.x, 0.5f, startPos.y), Quaternion.identity);

            yield return StartCoroutine(TrainAI());

            Destroy(aiAgent);

            // ���H�T�C�Y�𑝂₵�A�V�������H���쐬
            mazeSize += 2;
            Debug.Log($"�S�[���B���I �V�������H�T�C�Y: {mazeSize}�~{mazeSize}");

            yield return new WaitForSeconds(1.0f); // ���H�̃��Z�b�g��������悤�ɂ���

            GenerateMaze(); // �����ŐV�������H�𐶐�
        }
    }

    void GenerateMaze()
    {
        // ?? �ȑO�̖��H�̃I�u�W�F�N�g�����ׂč폜�i�X�^�[�g�����O�j
        foreach (Transform child in transform)
        {
            if (child.gameObject.tag != "Start") // �X�^�[�g�I�u�W�F�N�g���폜���Ȃ�
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

        // ?? �ȑO�̃S�[�������ׂč폜�i1�ł͂Ȃ������폜�j
        GameObject[] oldGoals = GameObject.FindGameObjectsWithTag("Goal");
        foreach (GameObject oldGoal in oldGoals)
        {
            Destroy(oldGoal);
        }

        // ?? �ȑO�̃X�^�[�g�������Ă���\�������邽�߁A�X�^�[�g���Đ���
        if (GameObject.FindGameObjectWithTag("Start") == null)
        {
            GameObject newStart = Instantiate(startPrefab, new Vector3(startPos.x, 0.5f, startPos.y), Quaternion.identity);
            newStart.tag = "Start"; // �^�O��ݒ�
        }

        // ?? �V�����S�[���𐶐����A�^�O��ݒ�
        GameObject newGoal = Instantiate(goalPrefab, new Vector3(goalPos.x, 0.5f, goalPos.y), Quaternion.identity);
        newGoal.tag = "Goal"; // �^�O��ݒ�
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
        Dictionary<Vector2Int, int> visitedCells = new Dictionary<Vector2Int, int>(); // �K��񐔂��L�^

        for (int episode = 0; episode < 100; episode++)
        {
            aiPos = startPos;
            aiAgent.transform.position = new Vector3(aiPos.x, 0.5f, aiPos.y);
            visitedCells.Clear(); // �K�◚�������Z�b�g

            while (aiPos != goalPos)
            {
                Action action = ChooseAction(aiPos);
                Vector2Int nextPos = aiPos + new Vector2Int(dx[(int)action], dy[(int)action]);

                if (maze[nextPos.x, nextPos.y] == 1)
                {
                    continue; // �ǂȂ�X�L�b�v
                }

                // �K��񐔂��J�E���g
                if (!visitedCells.ContainsKey(nextPos))
                    visitedCells[nextPos] = 0;
                visitedCells[nextPos]++;

                // �����ꏊ�ɖ߂�ƃy�i���e�B
                float revisitPenalty = visitedCells[nextPos] * -10.0f;

                // �S�[���ɋ߂Â������V�𑝂₷
                float goalDistanceBefore = Vector2Int.Distance(aiPos, goalPos);
                float goalDistanceAfter = Vector2Int.Distance(nextPos, goalPos);
                float distanceReward = (goalDistanceBefore - goalDistanceAfter) * 5; // �߂Â��قǕ�V����

                // ��{��V�ݒ�
                float reward = -1 + distanceReward + revisitPenalty;

                if (nextPos == goalPos)
                    reward = 100; // �S�[�����̕�V

                // Q�l�̍X�V
                float oldQ = qTable[aiPos.x, aiPos.y, (int)action];
                float maxNextQ = Mathf.Max(qTable[nextPos.x, nextPos.y, 0],
                                           qTable[nextPos.x, nextPos.y, 1],
                                           qTable[nextPos.x, nextPos.y, 2],
                                           qTable[nextPos.x, nextPos.y, 3]);

                qTable[aiPos.x, aiPos.y, (int)action] = oldQ + learningRate * (reward + discountFactor * maxNextQ - oldQ);

                aiPos = nextPos;
                aiAgent.transform.position = new Vector3(aiPos.x, 0.5f, aiPos.y);

                yield return new WaitForSeconds(0.02f);
            }

            explorationRate *= explorationDecay;
            // �S�[�������烋�[�v�𔲂���
            Debug.Log("AI���S�[�����܂����I");
            yield break;
        }
    }

    void SetupCamera()
    {
        Camera mainCamera = Camera.main;
        if (mainCamera != null)
        {
            float cameraHeight = mazeSize * 1.2f; // ���H�T�C�Y�ɍ��킹�č�����ύX
            mainCamera.transform.position = new Vector3(mazeSize / 2f, cameraHeight, mazeSize / 2f);
            mainCamera.transform.rotation = Quaternion.Euler(90, 0, 0);
            mainCamera.orthographic = true; // ���Ձi2D���_�j�ɂ���
            mainCamera.orthographicSize = mazeSize / 2f; // ���H�S�̂��f��
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
