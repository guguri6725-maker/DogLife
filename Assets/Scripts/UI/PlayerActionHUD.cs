using UnityEngine;
using UnityEngine.UI;

namespace DogLife
{
    public sealed class PlayerActionHUD : MonoBehaviour
    {
        public PlayerMotor motor;
        public Image cooldownShade;
        public Text remainingLabel;
        float displayed = -1;

        void LateUpdate()
        {
            if (!motor) return;
            float remaining = motor.DashRemaining;
            cooldownShade.fillAmount = Mathf.Clamp01(remaining / Mathf.Max(.01f, motor.dashCooldown));
            float tenths = Mathf.Ceil(remaining * 10) / 10;
            if (tenths == displayed) return;
            displayed = tenths;
            remainingLabel.text = remaining > 0 ? tenths.ToString("0.0") : "";
        }
    }
}
