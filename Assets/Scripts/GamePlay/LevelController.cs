using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UIElements;

public class LevelController : MonoBehaviour
{

    // Internal
    public bool idle;
    public PlayerMovement player;
    public List<PlayerMovement> mummies;
    public GameObject winGameScreen;
    public GameObject loseGameScreen;
    //public Stack checkpoints = new Stack();

    // Static
    public int size;
    float tranfromMap;
    int[,] verticalWall;
    int[,] horizontalWall;
    Vector3 stairPosition;
    Vector3 stairDirection;
    //bool restrictedVision = false;

    void Awake()
    {
        idle = true;
        mummies = new List<PlayerMovement>();
        verticalWall = new int[size, size];
        horizontalWall = new int[size, size];
        tranfromMap = size == 6 ? 1f : 0.75f;
    }

    void Start()
    {
        int n = size;
        foreach (Transform t in transform)
        {
            int x = (int)(t.localPosition.x/tranfromMap);
            int y = (int)(t.localPosition.y/tranfromMap);

            switch (t.tag)
            {
                case "Player":
                    player = t.GetComponent<PlayerMovement>();
                    break;
                case "MummyWhite":
                case "MummyRed":
                    mummies.Add(t.GetComponent<PlayerMovement>());
                    break;
                case "Stair":
                    stairPosition = t.localPosition;
                    if (x == 0) stairDirection = Vector3.left;
                    if (y == 0) stairDirection = Vector3.down;
                    if (x == n)
                    {
                        stairPosition.x--;
                        stairDirection = Vector3.right;
                    }
                    if (y == n)
                    {
                        stairPosition.y--;
                        stairDirection = Vector3.up;
                    }
                    break;
                case "vWall":
                    verticalWall[x, y] = 1;
                    break;
                case "hWall":
                    horizontalWall[x, y] = 1;
                    break;
                default:
                    Debug.Log("Unexpected game object with tag: " + t.tag);
                    break;
            }
        }
    }

    // Update is called once per frame
    void Update()
    {
        if(player == null || mummies == null)
        {
            return;
        }
        player.UpdateIdleDirection(null);
        foreach (var mummy in mummies)
        {
            Vector3 next_move = mummy.tag == "MummyWhite"
                ? WhiteTrace(mummy.transform.localPosition)
                : RedTrace(mummy.transform.localPosition);
            mummy.UpdateIdleDirection(next_move);
        }
        if (!idle) return;
        Vector3 direction = Vector3.zero;

        if (Input.GetKeyDown(KeyCode.W)) direction = Vector3.up;
        else if (Input.GetKeyDown(KeyCode.S)) direction = Vector3.down;
        else if (Input.GetKeyDown(KeyCode.A)) direction = Vector3.left;
        else if (Input.GetKeyDown(KeyCode.D)) direction = Vector3.right;

        if (direction != Vector3.zero)
            StartCoroutine(Action(direction));
    }

    IEnumerator Action(Vector3 direction)
    {

        // Player move 1 step
        if (Blocked(player.transform.localPosition, direction)) yield break;

        idle = false;
        yield return player.Move(direction, false);

        // Mummy move 2 step
        for (int step = 0; step < 2; step++)
        {
            yield return MummiesMove();

            if (MummiesCatch())
            {
                yield return Lost();
                yield break;
            }

            //yield return MummiesFight();
        }
        if (IsWin(player.transform.localPosition))
        {
            yield return Victory();
            yield break;
        }

        idle = true;
    }

    // Character vs walls
    bool Blocked(Vector3 position, Vector3 direction)
    {
        int x = (int)(position.x/tranfromMap);
        int y = (int)(position.y/tranfromMap);
        int n = size - 1;
        if (direction == Vector3.up)
            return y == n || horizontalWall[x, y + 1] == 1;

        if (direction == Vector3.down)
            return y == 0 || horizontalWall[x, y] == 1;

        if (direction == Vector3.left)
            return x == 0 || verticalWall[x, y] == 1;

        if (direction == Vector3.right)
            return x == n || verticalWall[x + 1, y] == 1;

        return true;
    }

    // Mummies    
    Vector3 WhiteTrace(Vector3 position)
    {
        int x = (int)(player.transform.localPosition.x / tranfromMap);
        int y = (int)(player.transform.localPosition.y / tranfromMap);
        int px = (int)(position.x / tranfromMap);
        int py = (int)(position.y / tranfromMap);

        if (x > px)
        {
            if (!Blocked(position, Vector3.right)) return Vector3.right;
        }
        if (x < px)
        {
            if (!Blocked(position, Vector3.left)) return Vector3.left;
        }
        if (y > py) return Vector3.up;
        if (y < py) return Vector3.down;
        if (x > px) return Vector3.right;
        if (x < px) return Vector3.left;

        return Vector3.zero;
    }

    Vector3 RedTrace(Vector3 position)
    {
        int x = (int)(player.transform.localPosition.x / tranfromMap);
        int y = (int)(player.transform.localPosition.y / tranfromMap);
        int px = (int)(position.x / tranfromMap);
        int py = (int)(position.y / tranfromMap);

        if (y > py)
        {
            if (!Blocked(position, Vector3.up)) return Vector3.up;
        }
        if (y < py)
        {
            if (!Blocked(position, Vector3.down)) return Vector3.down;
        }
        if (x > px) return Vector3.right;
        if (x < px) return Vector3.left;
        if (y > py) return Vector3.up;
        if (y < py) return Vector3.down;

        return Vector3.zero;
    }

    IEnumerator MummiesMove()
    {
        List<IEnumerator> coroutines = new List<IEnumerator>();

        foreach (var mummy in mummies)
        {
            Vector3 next_move = mummy.tag == "MummyRed"
                ? RedTrace(mummy.transform.localPosition)
                : WhiteTrace(mummy.transform.localPosition);

            bool isBlocked = Blocked(mummy.transform.localPosition, next_move);

            coroutines.Add(mummy.Move(next_move, isBlocked));
        }

        yield return StartCoroutine(PromiseAll(coroutines.ToArray()));
    }

    bool MummiesCatch()
    {
        foreach (var mummy in mummies)
        {
            if (mummy.transform.localPosition == player.transform.localPosition)        
                    return true;       
        }
        return false;
    }

    IEnumerator PromiseAll(params IEnumerator[] coroutines)
    {
        bool complete = false;
        while (!complete)
        {
            complete = true;

            foreach (IEnumerator x in coroutines)
            {
                if (x.MoveNext() == true)
                    complete = false;
                    yield return x.Current;
            }
            yield return null;
        }
    }

    // Win and lose
    bool IsWin(Vector3 position)
    {
        int x = (int)(position.x / tranfromMap);
        int y = (int)(position.y / tranfromMap);
        if (x == stairPosition.x &&  y == stairPosition.y)
        {
            return true;
        }
        return false;
    }

    IEnumerator Victory()
    {
        yield return player.Move(stairDirection, false);
        
        Destroy(player.gameObject);
        foreach (var mummy in mummies)
            Destroy(mummy.gameObject);
        mummies.Clear();

        yield return new WaitForSeconds(0.5f);
        Vector3 position = new Vector3(3, 3.33f, 0);
        Quaternion rotation = Quaternion.identity;
        Instantiate(winGameScreen, position, rotation);
    }
    IEnumerator Lost()
    {
        Destroy(player.gameObject);
        foreach (var mummy in mummies) 
        {
            mummy.Fighting();
            yield return new WaitForSeconds(1.5f);
            Destroy(mummy.gameObject); 
        }    
        mummies.Clear();
        yield return new WaitForSeconds(1f);
        Vector3 position = new Vector3(2, 3, 0);
        Quaternion rotation = Quaternion.identity;    
        Instantiate(loseGameScreen, position, rotation);
    }
}