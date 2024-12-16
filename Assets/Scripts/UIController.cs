/// <summary>
/// This script belongs to cowsins™ as a part of the cowsins´ FPS Engine. All rights reserved. 
/// </summary>
#if UNITY_EDITOR
using UnityEditor;
#endif
using UnityEngine;
using System;
using TMPro;
using UnityEngine.UI;
using UnityEngine.SceneManagement;
using System.Collections.Generic;
using HEAVYART.TopDownShooter.Netcode;
using Unity.Netcode;

namespace cowsins {
    /// <summary>
    /// Manage UI actions.
    /// This is still subject to change and optimize.
    /// </summary>
    public class UIController : NetworkBehaviour {
        public EndOfGamePopupUIController endOfGamePopup;
        public PlayerMovement playerMovement;

        [Tooltip("Use image bars to display player statistics.")] public bool barHealthDisplay;

        [Tooltip("Use text to display player statistics.")] public bool numericHealthDisplay;

        private Action<float, float> healthDisplayMethod;

        [Tooltip("Slider that will display the health on screen"), SerializeField] private Slider healthSlider;

        [Tooltip("Slider that will display the shield on screen"), SerializeField] private Slider shieldSlider;

        [SerializeField, Tooltip("UI Element ( TMPro text ) that displays current and maximum health.")] private TextMeshProUGUI healthTextDisplay;

        [SerializeField, Tooltip("UI Element ( TMPro te¡xt ) that displays current and maximum shield.")] private TextMeshProUGUI shieldTextDisplay;

        [Tooltip("This image shows damage and heal states visually on your screen, you can change the image" +
                "to any you like, but note that color will be overriden by the script"), SerializeField]
        private Image healthStatesEffect;

        [Tooltip(" Color of healthStatesEffect on different actions such as being hurt or healed"), SerializeField] private Color damageColor, healColor, coinCollectColor, xpCollectColor;

        [Tooltip("Time for the healthStatesEffect to fade out"), SerializeField] private float fadeOutTime;

        [Tooltip("An object showing death events will be displayed on kill")] public bool displayEvents;

        [Tooltip("UI element which contains the killfeed. Where the kilfeed object will be instantiated and parented to"), SerializeField]
        private GameObject killfeedContainer;

        [Tooltip("Object to spawn"), SerializeField] private GameObject killfeedObject;

        [Tooltip("Attach the UI you want to use as your interaction UI")] public GameObject interactUI;

        [Tooltip("Displays the current progress of your interaction"), SerializeField] private Image interactUIProgressDisplay;

        [SerializeField, Tooltip("UI that displays incompatible interactions.")] private GameObject forbiddenInteractionUI;

        [Tooltip("Inside the interact UI, this is the text that will display the object you want to interact with " +
           "or any custom method you would like." +
           "Do check Interactable.cs for that or, if you want, read our documentation or contact the cowsins support " +
           "in order to make custom interactions."), SerializeField]
        private TextMeshProUGUI interactText;

        [Tooltip("UI enabled when inspecting.")] public CanvasGroup inspectionUI;

        [SerializeField, Tooltip("Text that displays the name of the current weapon when inspecting.")] private TextMeshProUGUI weaponDisplayText_AttachmentsUI;

        [SerializeField, Tooltip("Prefab of the UI element that represents an attachment on-screen when inspecting")] private GameObject attachmentDisplay_UIElement;

        [SerializeField, Tooltip("Group of attachments. Attachment UI elements are wrapped inside these.")]
        private GameObject
            barrels_AttachmentsGroup,
            scopes_AttachmentsGroup,
            stocks_AttachmentsGroup,
            grips_AttachmentsGroup,
            magazines_AttachmentsGroup,
            flashlights_AttachmentsGroup,
            lasers_AttachmentsGroup;

        [SerializeField, Tooltip("Color of an attachment UI element when it is equipped.")] private Color usingAttachmentColor;

        [SerializeField, Tooltip("Color of an attachment UI element when it is unequipped. This is the default color.")] private Color notUsingAttachmentColor;

        [SerializeField, Tooltip("Contains dashUIElements in game.")] private Transform dashUIContainer;

        [SerializeField, Tooltip("Displays a dash slot in-game. This keeps stored at dashUIContainer during runtime.")] private Transform dashUIElement;

        [Tooltip("Attach the appropriate UI here")] public TextMeshProUGUI bulletsUI, magazineUI, reloadUI, lowAmmoUI;

        [Tooltip("Display an icon of your current weapon")] public Image currentWeaponDisplay;

        [Tooltip("Image that represents heat levels of your overheating weapon"), SerializeField] private Image overheatUI;

        [Tooltip(" Attach the CanvasGroup that contains the inventory")] public CanvasGroup inventoryContainer;

        [SerializeField] private GameObject coinsUI;

        [SerializeField] private TextMeshProUGUI coinsText;

        [SerializeField] private Hitmarker hitmarker;

        public Crosshair crosshair;

        [SerializeField] private Image xpImage;

        [SerializeField] private TextMeshProUGUI currentLevel, nextLevel;

        [SerializeField] private float lerpXpSpeed;


        public delegate void AddXP();

        public static AddXP addXP;


        private void Start() {
            if(IsOwner) {
                gameObject.SetActive(true);
                GameManager.Instance.OnGameEnd += ShowEndOfGamePopup;
            } else {
                gameObject.SetActive(false);
            }
        }

        public void ShowEndOfGamePopup() {
            HidePopups();
            HideStats();
            endOfGamePopup.gameObject.SetActive(true);
        }

        void HideStats() {
            if(healthSlider != null) {
                healthSlider.gameObject.SetActive(false);
                bulletsUI.gameObject.SetActive(false);
                magazineUI.gameObject.SetActive(false);
                currentWeaponDisplay.gameObject.SetActive(false);
            }
        }

        public void HidePopups() {
            if(endOfGamePopup == null) return;
            endOfGamePopup.gameObject.SetActive(false);
            //quitGamePopup.gameObject.SetActive(false);
        }

        private void Update() {
            if(healthStatesEffect.color != new Color(healthStatesEffect.color.r,
                healthStatesEffect.color.g,
                healthStatesEffect.color.b, 0)) healthStatesEffect.color -= new Color(0, 0, 0, Time.deltaTime * fadeOutTime);


            //Inventory
            //if ((InputManager.scrolling != 0 || InputManager.nextweapon || InputManager.previousweapon) && !InputManager.reloading) inventoryContainer.alpha = 1;
            //else if (inventoryContainer.alpha > 0) inventoryContainer.alpha -= Time.deltaTime;
        }

        // HEALTH SYSTEM /////////////////////////////////////////////////////////////////////////////////////////
        public void UpdateHealthUI(float health, float shield, bool damaged) {
            BarHealthDisplayMethod(health, shield);
            NumericHealthDisplayMethod(health, shield);
        }

        public void HealthSetUp(float health, float shield, float maxHealth, float maxShield) {
            if(healthSlider != null) {
                healthSlider.maxValue = maxHealth;
            }
            if(shieldSlider != null) {
                shieldSlider.maxValue = maxShield;
            }

            healthDisplayMethod?.Invoke(health, shield);

            if(shield == 0) shieldSlider.gameObject.SetActive(false);
        }

        private void BarHealthDisplayMethod(float health, float shield) {
            if(healthSlider != null)
                healthSlider.value = health;

            if(shieldSlider != null)
                shieldSlider.value = shield;
        }
        private void NumericHealthDisplayMethod(float health, float shield) {
            if(healthTextDisplay != null) {
                healthTextDisplay.text = health > 0 && health <= 1 ? 1.ToString("F0") : health.ToString("F0");
            }

            if(shieldTextDisplay != null)
                shieldTextDisplay.text = shield.ToString("F0");
        }

        // INTERACTION /////////////////////////////////////////////////////////////////////////////////////////
        private void AllowedInteraction(string displayText) {
            forbiddenInteractionUI.SetActive(false);
            interactUI.SetActive(true);
            interactText.text = displayText;
            interactUI.GetComponent<Animation>().Play();
            interactUI.GetComponent<AudioSource>().Play();

            // Adjust the width of the background based on the length of the displayText
            RectTransform imageRect = interactUI.GetComponentInChildren<Image>().GetComponent<RectTransform>();
            float textLength = displayText.Length;
            imageRect.sizeDelta = new Vector2(100 + textLength * 10, imageRect.sizeDelta.y);
        }

        private void ForbiddenInteraction() {
            forbiddenInteractionUI.SetActive(true);
            interactUI.SetActive(false);
        }

        private void DisableInteractionUI() {
            forbiddenInteractionUI.SetActive(false);
            interactUI.SetActive(false);
        }
        private void InteractioProgressUpdate(float value) {
            interactUIProgressDisplay.gameObject.SetActive(true);
            interactUIProgressDisplay.fillAmount = value;
        }
        private void FinishInteraction() {
            interactUIProgressDisplay.gameObject.SetActive(false);
        }

        // UI EVENTS /////////////////////////////////////////////////////////////////////////////////////////
        public void AddKillfeed(string name) {
            GameObject killfeed = Instantiate(killfeedObject, transform.position, Quaternion.identity, killfeedContainer.transform);
            killfeed.transform.GetChild(0).Find("Text").GetComponent<TextMeshProUGUI>().text = "You killed: " + name;
        }

        public void Hitmarker(bool headshot) {
            hitmarker.Play(headshot);
        }

        // MOVEMENT    ////////////////////////////////////////////////////////////////////////////////////////

        private List<GameObject> dashElements; // Stores the UI Elements required to display the current dashes amount

        /// <summary>
        /// Draws the dash UI 
        /// </summary>
        private void DrawDashUI(int amountOfDashes) {
            dashElements = new List<GameObject>(amountOfDashes);
            int i = 0;
            while(i < amountOfDashes) {
                var uiElement = Instantiate(dashUIElement, dashUIContainer);
                dashElements.Add(uiElement.gameObject);
                i++;
            }
        }

        private void RegainDash() {
            // Enable a new UI Element
            var uiElement = Instantiate(dashUIElement, dashUIContainer);
            dashElements.Add(uiElement.gameObject);
        }

        private void DashUsed(int currentDashes) {
            // Remove the UI Element
            var element = dashElements[currentDashes];
            dashElements.Remove(element);
            Destroy(element);
        }

        // WEAPON    /////////////////////////////////////////////////////////////////////////////////////////

        public void DetectReloadMethod(bool enable, bool useOverheat) {
            bulletsUI.gameObject.SetActive(enable);
            magazineUI.gameObject.SetActive(enable);
            overheatUI.transform.parent.gameObject.SetActive(useOverheat);
        }

        public void UpdateHeatRatio(float heatRatio) {
            overheatUI.fillAmount = heatRatio;
        }
        public void UpdateBullets(int bullets, int mag, bool activeReloadUI, bool activeLowAmmoUI) {
            bulletsUI.text = bullets.ToString();
            magazineUI.text = mag.ToString();
            reloadUI.gameObject.SetActive(activeReloadUI);
            lowAmmoUI.gameObject.SetActive(activeLowAmmoUI);
        }
        public void DisableWeaponUI() {
            overheatUI.transform.parent.gameObject.SetActive(false);
            bulletsUI.gameObject.SetActive(false);
            magazineUI.gameObject.SetActive(false);
            currentWeaponDisplay.gameObject.SetActive(false);
            reloadUI.gameObject.SetActive(false);
            lowAmmoUI.gameObject.SetActive(false);
        }

        public void SetWeaponDisplay(Weapon_SO weapon) => currentWeaponDisplay.sprite = weapon.icon;

        public void EnableDisplay() => currentWeaponDisplay.gameObject.SetActive(true);

        // OTHERS    /////////////////////////////////////////////////////////////////////////////////////////

        public void ChangeScene(int scene) => SceneManager.LoadScene(scene);

        private void OnEnable() {
            UIEvents.onHealthChanged += UpdateHealthUI;
            UIEvents.basicHealthUISetUp += HealthSetUp;
            healthDisplayMethod += BarHealthDisplayMethod;
            healthDisplayMethod += NumericHealthDisplayMethod;
            UIEvents.allowedInteraction += AllowedInteraction;
            UIEvents.forbiddenInteraction += ForbiddenInteraction;
            UIEvents.disableInteractionUI += DisableInteractionUI;
            UIEvents.onInteractionProgressChanged += InteractioProgressUpdate;
            UIEvents.onFinishInteractionProgress += FinishInteraction;
            UIEvents.onInitializeDashUI += DrawDashUI;
            UIEvents.onDashGained += RegainDash;
            UIEvents.onDashUsed += DashUsed;
            UIEvents.onEnemyHit += Hitmarker;
            UIEvents.onEnemyKilled += AddKillfeed;
            UIEvents.onDetectReloadMethod += DetectReloadMethod;
            UIEvents.onHeatRatioChanged += UpdateHeatRatio;
            UIEvents.onBulletsChanged += UpdateBullets;
            UIEvents.disableWeaponUI += DisableWeaponUI;
            UIEvents.setWeaponDisplay += SetWeaponDisplay;
            UIEvents.enableWeaponDisplay += EnableDisplay;

            interactUI.SetActive(false);
        }
        private void OnDisable() {
            UIEvents.onHealthChanged = null;
            UIEvents.basicHealthUISetUp = null;
            healthDisplayMethod = null;
            UIEvents.allowedInteraction = null;
            UIEvents.forbiddenInteraction = null;
            UIEvents.disableInteractionUI = null;
            UIEvents.onInteractionProgressChanged = null;
            UIEvents.onFinishInteractionProgress = null;
            UIEvents.onGenerateInspectionUI = null;
            UIEvents.onInitializeDashUI = null;
            UIEvents.onDashGained = null;
            UIEvents.onDashUsed = null;
            UIEvents.onEnemyHit = null;
            UIEvents.onEnemyKilled = null;
            UIEvents.onDetectReloadMethod = null;
            UIEvents.onHeatRatioChanged = null;
            UIEvents.onBulletsChanged = null;
            UIEvents.disableWeaponUI = null;
            UIEvents.setWeaponDisplay = null;
            UIEvents.enableWeaponDisplay = null;
            addXP = null;
        }

    }
}