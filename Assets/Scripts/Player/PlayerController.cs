using UnityEngine;
using MyPokemon.Protocol;
using TMPro;

public class PlayerController : MonoBehaviour
{
    private float moveSpeed = 5f;
    private float updateInterval = 0.1f; // 每0.1秒发送一次位置
    private float updateTimer = 0f;
    private Vector3 lastPosition;
    private SpriteRenderer spriteRenderer;
    private Animator animator; // 如果有动画
    private MoveDirection currentDirection = MoveDirection.None;
    private MotionStates currentMotionState = MotionStates.Idle;

    private void Start()
    {
        lastPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();

        // 设置初始位置和朝向
        if (PlayerData.Instance != null)
        {
            transform.position = PlayerData.Instance.GetPosition();
            currentDirection = PlayerData.Instance.Direction;
            
            // 如果有名字文本组件，设置玩家名字
            if (TryGetComponent<TextMeshPro>(out var nameText))
            {
                nameText.text = PlayerData.Instance.PlayerName;
            }

            // 更新动画状态
            if (animator != null)
            {
                animator.SetInteger("Direction", (int)currentDirection);
                animator.SetInteger("MotionState", (int)currentMotionState);
            }

            GameLogger.LogPlayer($"初始化玩家 - 名字: {PlayerData.Instance.PlayerName}, " +
                               $"位置: ({PlayerData.Instance.PositionX:F2}, {PlayerData.Instance.PositionY:F2}), " +
                               $"朝向: {currentDirection}, 状态: {currentMotionState}");
        }
        else
        {
            GameLogger.LogError("PlayerData.Instance is null!");
        }
    }

    private void Update()
    {
        // 获取输入
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 计算移动
        Vector3 movement = new Vector3(horizontal, vertical, 0f);
        transform.position += movement * moveSpeed * Time.deltaTime;

        // 更新朝向和状态
        UpdateDirectionAndState(horizontal, vertical);

        // 发送位置更新（即使没有移动也发送，以保持连接活跃）
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)  // updateInterval 可以设置为 0.1f，表示每100ms更新一次
        {
            //GameLogger.LogPlayer($"发送位置更新 - 位置: ({transform.position.x:F2}, {transform.position.y:F2}), 朝向: {currentDirection}");
            NetworkManager.Instance?.SendPosition(transform.position.x, transform.position.y, currentDirection);
            updateTimer = 0f;
        }
    }

    private void UpdateDirectionAndState(float horizontal, float vertical)
    {
        // 更新朝向
        if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
        {
            currentDirection = horizontal > 0 ? MoveDirection.Right : MoveDirection.Left;
        }
        else if (Mathf.Abs(vertical) > 0.1f)
        {
            currentDirection = vertical > 0 ? MoveDirection.Up : MoveDirection.Down;
        }

        // 更新运动状态
        if (Mathf.Abs(horizontal) > 0.1f || Mathf.Abs(vertical) > 0.1f)
        {
            currentMotionState = MotionStates.Movement;
        }
        else
        {
            currentMotionState = MotionStates.Idle;
        }

        // 更新动画
        if (animator != null)
        {
            animator.SetInteger("Direction", (int)currentDirection);
            animator.SetInteger("MotionState", (int)currentMotionState);
        }

        // 处理精灵翻转
        if (spriteRenderer != null && currentDirection != MoveDirection.None)
        {
            spriteRenderer.flipX = currentDirection == MoveDirection.Left;
        }
    }
}