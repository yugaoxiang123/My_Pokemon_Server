using UnityEngine;
using MyPokemon.Protocol;

public class PlayerController : MonoBehaviour
{
    private float moveSpeed = 5f;
    private float updateInterval = 0.1f; // 每0.1秒发送一次位置
    private float updateTimer = 0f;
    private Vector3 lastPosition;
    private SpriteRenderer spriteRenderer;
    private Animator animator; // 如果有动画
    private int currentDirection = 0; // Assuming 0 is up

    private void Start()
    {
        lastPosition = transform.position;
        spriteRenderer = GetComponent<SpriteRenderer>();
        animator = GetComponent<Animator>();
    }

    private void Update()
    {
        // 获取输入
        float horizontal = Input.GetAxis("Horizontal");
        float vertical = Input.GetAxis("Vertical");

        // 计算移动
        Vector3 movement = new Vector3(horizontal, vertical, 0f);
        transform.position += movement * moveSpeed * Time.deltaTime;

        // 更新朝向
        int direction = GetDirection(horizontal, vertical);
        if (direction != -1)
        {
            currentDirection = direction;
        }

        // 发送位置更新（即使没有移动也发送，以保持连接活跃）
        updateTimer += Time.deltaTime;
        if (updateTimer >= updateInterval)  // updateInterval 可以设置为 0.1f，表示每100ms更新一次
        {
            GameLogger.LogPlayer($"发送位置更新 - 位置: ({transform.position.x:F2}, {transform.position.y:F2}), 朝向: {currentDirection}");
            NetworkManager.Instance?.SendPosition(transform.position.x, transform.position.y, currentDirection);
            updateTimer = 0f;
        }
    }

    private int GetDirection(float horizontal, float vertical)
    {
        if (Mathf.Abs(horizontal) > Mathf.Abs(vertical))
        {
            return horizontal > 0 ? 1 : 3;
        }
        else if (vertical != 0)
        {
            return vertical > 0 ? 0 : 2;
        }
        return -1;
    }

    private void UpdateAnimation(Vector3 movement)
    {
        // 更新动画参数
        if (animator != null)
        {
            animator.SetInteger("Direction", currentDirection);
            animator.SetBool("IsMoving", movement != Vector3.zero);
        }

        // 如果需要翻转精灵
        if (movement.x != 0)
        {
            spriteRenderer.flipX = movement.x < 0;
        }
    }
} 