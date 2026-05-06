using UnityEngine;

public class AnimationEventRelay : MonoBehaviour
{
    // 在您的案例中，这里应该是 PlayerMovement
    // 您可以根据您脚本的实际名称修改它
    private MechController playerMovement;

    void Awake()
    {
        // 在父级物体中寻找 MechController 脚本
        playerMovement = GetComponentInParent<MechController>();
        // 影分身等场景下可能没有 MechController，不需要报错
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