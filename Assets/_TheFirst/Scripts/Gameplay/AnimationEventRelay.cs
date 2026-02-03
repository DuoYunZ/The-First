using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    // 在您的案例中，这里应该是 PlayerMovement
    // 您可以根据您脚本的实际名称修改它
    private MechController playerMovement;

    void Awake()
    {
        // 在父级物体中寻找 PlayerMovement 脚本
        playerMovement = GetComponentInParent<MechController>();
        if (playerMovement == null)
        {
            Debug.LogError("在父级中找不到 PlayerMovement 脚本!", this);
        }
    }

    // 这个方法将由动画事件来调用
    public void RelayPlayFootstepSound()
    {
        // 确保找到了父级脚本，然后调用它的方法
        if (playerMovement != null)
        {
            playerMovement.PlayFootstepSound();
        }
    }

    // 如果未来您有其他需要转发的动画事件，可以在下面继续添加
    // public void RelayAttackEvent()
    // {
    //     if (playerMovement != null)
    //     {
    //         playerMovement.HandleAttack();
    //     }
    // }
}