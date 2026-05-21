using System.Collections;
using System.Collections.Generic;
using System.Linq;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Lightweight in-run role identity layer.
/// Mage: energy active cast. Swordsman: no-damage blade focus stacks. Engineer: parts auto-modify mechanical weapons.
/// </summary>
public class PlayerRoleRuntimeMechanics : MonoBehaviour
{
    public static PlayerRoleRuntimeMechanics Instance { get; private set; }

    private enum RoleKind
    {
        None,
        Swordsman,
        Mage,
        Engineer
    }

    [Header("通用资源")]
    public float resourceMax = 100f;

    [Header("法师能量施法")]
    public float mageEnergyPerKill = 4f;
    public float mageEnergyPerLevel = 22f;
    public float mageBurstRadius = 9f;
    public int mageBurstBaseDamage = 85;
    public float mageBurstLevelDamage = 7f;

    [Header("剑士剑势")]
    public float swordFocusInterval = 30f;
    public int swordFocusMaxStacks = 3;
    public float swordFocusAreaPerStack = 0.30f;
    public float swordFocusFinalStackArea = 0.50f;

    [Header("HUD")]
    public RoleMechanicHudView roleHudPrefab;

    [Header("工程师零件")]
    public int engineerPartsPerKill = 1;
    public int engineerEliteParts = 5;
    public int engineerPartsPerModification = 18;
    public int engineerModificationLevelGain = 1;

    private CharacterData characterData;
    private RoleKind roleKind = RoleKind.None;
    private float resource;
    private int parts;
    private int swordFocusStacks;
    private float swordFocusTimer;
    private float swordBurstRadius = 7.5f;
    private int swordBurstBaseDamage = 70;
    private float swordSurgeDuration = 6f;
    private bool swordSurgeActive;
    private Coroutine swordSurgeRoutine;
    private readonly Dictionary<WeaponPart, float> swordFocusAppliedBonuses = new Dictionary<WeaponPart, float>();

    private Canvas hudCanvas;
    private TextMeshProUGUI labelText;
    private Image fillImage;
    private RoleMechanicHudView hudView;

    private const string DefaultRoleHudPrefabPath = "Assets/_TheFirst/Prefabs/UI/RoleMechanicHUD.prefab";

    public void Initialize(CharacterData data)
    {
        characterData = data;
        roleKind = ResolveRoleKind(data);
        resource = 0f;
        parts = 0;
        swordFocusStacks = 0;
        swordFocusTimer = 0f;

        BuildHud();
        RefreshHud();

        Health.OnEnemyDied -= HandleEnemyDied;
        Health.OnEnemyDied += HandleEnemyDied;
        Health.OnPlayerHealthDamaged -= HandlePlayerHealthDamaged;
        Health.OnPlayerHealthDamaged += HandlePlayerHealthDamaged;

        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp -= HandleLevelUp;
            PlayerLevelManager.Instance.OnLevelUp += HandleLevelUp;
        }

        Debug.Log($"<color=cyan>[RoleMechanic] Initialized {roleKind} for {data?.characterName}</color>");
    }

    private void Awake()
    {
        if (Instance != null && Instance != this)
        {
            Destroy(this);
            return;
        }
        Instance = this;
    }

    private void OnDestroy()
    {
        Health.OnEnemyDied -= HandleEnemyDied;
        Health.OnPlayerHealthDamaged -= HandlePlayerHealthDamaged;
        if (PlayerLevelManager.Instance != null)
        {
            PlayerLevelManager.Instance.OnLevelUp -= HandleLevelUp;
        }
        if (Instance == this) Instance = null;
    }

    private void Update()
    {
        if (roleKind != RoleKind.Swordsman) return;

        UpdateSwordFocusAreaBonus();

        if (swordFocusStacks >= swordFocusMaxStacks)
        {
            swordFocusTimer = swordFocusInterval;
            RefreshHud();
            return;
        }

        swordFocusTimer += Time.deltaTime;
        if (swordFocusTimer >= swordFocusInterval)
        {
            swordFocusTimer -= swordFocusInterval;
            SetSwordFocusStacks(swordFocusStacks + 1);
        }
        else
        {
            RefreshHud();
        }
    }

    private void HandlePlayerHealthDamaged(int damage)
    {
        if (roleKind != RoleKind.Swordsman || damage <= 0) return;

        if (swordFocusStacks > 0)
        {
            SetSwordFocusStacks(swordFocusStacks - 1);
        }
        swordFocusTimer = 0f;
        RefreshHud();
    }

    private void SetSwordFocusStacks(int stacks)
    {
        swordFocusStacks = Mathf.Clamp(stacks, 0, swordFocusMaxStacks);
        UpdateSwordFocusAreaBonus();
        RefreshHud();
    }

    private float GetSwordFocusAreaBonus()
    {
        if (swordFocusStacks <= 0) return 0f;
        if (swordFocusStacks >= 3)
        {
            return swordFocusAreaPerStack * 2f + swordFocusFinalStackArea;
        }

        return swordFocusAreaPerStack * swordFocusStacks;
    }

    private void UpdateSwordFocusAreaBonus()
    {
        if (WeaponController.Instance == null) return;

        float desiredBonus = GetSwordFocusAreaBonus();
        List<WeaponPart> currentSlashParts = GetOwnedWeaponParts()
            .Where(part => part != null && WeaponBuildTagUtility.IsSlashWeapon(part.StatBlock))
            .ToList();

        List<WeaponPart> tracked = swordFocusAppliedBonuses.Keys.ToList();
        bool changed = false;
        foreach (WeaponPart part in tracked)
        {
            if (part == null || !currentSlashParts.Contains(part))
            {
                swordFocusAppliedBonuses.Remove(part);
                changed = true;
            }
        }

        foreach (WeaponPart part in currentSlashParts)
        {
            float currentApplied = swordFocusAppliedBonuses.TryGetValue(part, out float value) ? value : 0f;
            float delta = desiredBonus - currentApplied;
            if (Mathf.Abs(delta) <= 0.0001f) continue;

            part.localAreaBonus += delta;
            swordFocusAppliedBonuses[part] = desiredBonus;
            changed = true;
        }

        if (changed)
        {
            WeaponController.Instance.RefreshAllWeaponStates();
        }
    }

    private RoleKind ResolveRoleKind(CharacterData data)
    {
        string id = data != null ? data.characterID : "";
        if (id == "Role02") return RoleKind.Mage;
        if (id == "Role03") return RoleKind.Engineer;
        if (id == "Role01") return RoleKind.Swordsman;
        return RoleKind.None;
    }

    private void HandleEnemyDied(Health enemy)
    {
        if (enemy == null || roleKind == RoleKind.None) return;

        switch (roleKind)
        {
            case RoleKind.Mage:
                AddResource(GetMageEnergyGain(mageEnergyPerKill));
                break;
            case RoleKind.Engineer:
                AddParts(engineerPartsPerKill + (enemy.isElite ? engineerEliteParts : 0));
                break;
        }
    }

    private void HandleLevelUp(int newLevel)
    {
        if (roleKind == RoleKind.Mage)
        {
            AddResource(GetMageEnergyGain(mageEnergyPerLevel));
        }
    }

    private float GetMageEnergyGain(float baseGain)
    {
        float gain = baseGain;
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Mage_EnergyBloom"))
        {
            gain *= 1.5f;
        }
        return gain;
    }

    private void AddResource(float amount)
    {
        resource = Mathf.Clamp(resource + amount, 0f, resourceMax);
        RefreshHud();
    }

    private void AddParts(int amount)
    {
        int multiplier = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Engineer_ScrapRecycler") ? 2 : 1;
        parts += Mathf.Max(0, amount * multiplier);

        int threshold = GetEngineerModificationThreshold();
        while (parts >= threshold)
        {
            parts -= threshold;
            ApplyEngineerModification();
            threshold = GetEngineerModificationThreshold();
        }

        RefreshHud();
    }

    private int GetEngineerModificationThreshold()
    {
        int threshold = engineerPartsPerModification;
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Engineer_OverclockWorkshop"))
        {
            threshold = Mathf.Max(8, threshold - 5);
        }
        return threshold;
    }

    public bool TryUseActiveSkill()
    {
        if (roleKind == RoleKind.Mage)
        {
            if (resource < resourceMax) return false;
            resource = 0f;
            RefreshHud();
            StartCoroutine(CastMageEnergyBurst());
            return true;
        }

        if (roleKind == RoleKind.Swordsman)
        {
            return false;
        }

        if (roleKind == RoleKind.Engineer)
        {
            if (parts < GetEngineerModificationThreshold()) return false;
            parts -= GetEngineerModificationThreshold();
            ApplyEngineerModification();
            RefreshHud();
            return true;
        }

        return false;
    }

    private IEnumerator CastMageEnergyBurst()
    {
        int currentLevel = PlayerLevelManager.Instance != null ? PlayerLevelManager.Instance.GetLevel() : 1;
        int damage = mageBurstBaseDamage + Mathf.RoundToInt(currentLevel * mageBurstLevelDamage);
        int pulses = UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Mage_ArcaneNova") ? 3 : 2;

        for (int i = 0; i < pulses; i++)
        {
            DamageEnemiesInRadius(mageBurstRadius + i * 1.5f, damage, "能量施法");
            SpawnPulseVfx(new Color(0.35f, 0.78f, 1f, 0.34f), mageBurstRadius + i * 1.5f);
            yield return new WaitForSeconds(0.22f);
        }
    }

    private void CastSwordMomentumBurst()
    {
        int currentLevel = PlayerLevelManager.Instance != null ? PlayerLevelManager.Instance.GetLevel() : 1;
        int damage = swordBurstBaseDamage + currentLevel * 5;
        DamageEnemiesInRadius(swordBurstRadius, damage, "剑势爆发");
        SpawnPulseVfx(new Color(1f, 0.72f, 0.22f, 0.34f), swordBurstRadius);

        if (!swordSurgeActive)
        {
            if (swordSurgeRoutine != null) StopCoroutine(swordSurgeRoutine);
            swordSurgeRoutine = StartCoroutine(SwordSurgeBuff());
        }
    }

    private IEnumerator SwordSurgeBuff()
    {
        if (swordSurgeActive) yield break;
        swordSurgeActive = true;

        List<WeaponPart> buffed = GetOwnedWeaponParts()
            .Where(part => part != null && WeaponBuildTagUtility.IsSlashWeapon(part.StatBlock))
            .ToList();

        foreach (WeaponPart part in buffed)
        {
            part.localDamageBonus += 0.25f;
            part.localAreaBonus += 0.20f;
        }
        WeaponController.Instance?.RefreshAllWeaponStates();

        yield return new WaitForSeconds(swordSurgeDuration);

        foreach (WeaponPart part in buffed)
        {
            if (part == null) continue;
            part.localDamageBonus -= 0.25f;
            part.localAreaBonus -= 0.20f;
        }
        WeaponController.Instance?.RefreshAllWeaponStates();
        swordSurgeActive = false;
    }

    private void ApplyEngineerModification()
    {
        if (WeaponController.Instance == null) return;

        List<OwnedWeapon> candidates = WeaponController.Instance.ownedWeapons
            .Where(w => w != null && w.weaponPartInstance != null && WeaponBuildTagUtility.IsMechanicalWeapon(w.stats))
            .ToList();

        if (candidates.Count == 0)
        {
            candidates = WeaponController.Instance.ownedWeapons
                .Where(w => w != null && w.weaponPartInstance != null)
                .ToList();
        }
        if (candidates.Count == 0) return;

        OwnedWeapon selected = candidates[Random.Range(0, candidates.Count)];
        int levelGain = engineerModificationLevelGain;
        if (UpgradeManager.Instance != null && UpgradeManager.Instance.HasActiveCharacterSkill("Engineer_OverclockWorkshop"))
        {
            levelGain++;
        }

        int applied = WeaponController.Instance.GrantWeaponLevels(selected, levelGain);
        if (applied <= 0 && selected.weaponPartInstance != null)
        {
            selected.weaponPartInstance.localDamageBonus += 0.08f;
            selected.weaponPartInstance.localFireRateBonus += 0.06f;
            WeaponController.Instance.RefreshAllWeaponStates();
        }

        Debug.Log($"<color=#9BE7FF>[工程改造] {selected.stats.weaponName} 改造完成，等级+{applied}</color>");
    }

    private IEnumerable<WeaponPart> GetOwnedWeaponParts()
    {
        if (WeaponController.Instance == null) yield break;
        foreach (OwnedWeapon owned in WeaponController.Instance.ownedWeapons)
        {
            if (owned != null && owned.weaponPartInstance != null)
            {
                yield return owned.weaponPartInstance;
            }
        }
    }

    private void DamageEnemiesInRadius(float radius, int damage, string sourceName)
    {
        Collider[] hits = Physics.OverlapSphere(transform.position, radius);
        HashSet<Health> damaged = new HashSet<Health>();
        foreach (Collider hit in hits)
        {
            Health health = hit.GetComponentInParent<Health>();
            if (health == null || health.IsDead || health.isPlayerHealth || damaged.Contains(health)) continue;
            damaged.Add(health);
            health.TakeDamage(damage, health.transform.position, gameObject, AttackType.Standard, null, null, sourceName);
        }
    }

    private void SpawnPulseVfx(Color color, float radius)
    {
        GameObject pulse = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        pulse.name = "RoleMechanicPulse";
        pulse.transform.position = transform.position + Vector3.up * 0.15f;
        pulse.transform.localScale = new Vector3(radius * 2f, 0.08f, radius * 2f);
        Collider col = pulse.GetComponent<Collider>();
        if (col != null) Destroy(col);

        Renderer renderer = pulse.GetComponent<Renderer>();
        if (renderer != null)
        {
            Shader shader = Shader.Find("Universal Render Pipeline/Unlit");
            if (shader == null) shader = Shader.Find("Sprites/Default");
            if (shader == null) shader = Shader.Find("Standard");
            if (shader != null)
            {
                Material mat = new Material(shader);
                mat.color = color;
                renderer.material = mat;
            }
        }
        Destroy(pulse, 0.35f);
    }

    private void BuildHud()
    {
        if (roleKind == RoleKind.None) return;

        RoleMechanicHudView prefab = ResolveRoleHudPrefab();
        if (prefab != null)
        {
            hudView = Instantiate(prefab, transform);
            hudView.name = "RoleMechanicHUD";
            hudCanvas = hudView.GetComponent<Canvas>();
            hudView.ConfigureForRole(roleKind.ToString(), GetRoleColor());
            labelText = hudView.LabelText;
            fillImage = hudView.FillImage;
            return;
        }

        BuildRuntimeHudFallback();
    }

    private RoleMechanicHudView ResolveRoleHudPrefab()
    {
        if (roleHudPrefab != null) return roleHudPrefab;

#if UNITY_EDITOR
        GameObject prefabObject = AssetDatabase.LoadAssetAtPath<GameObject>(DefaultRoleHudPrefabPath);
        if (prefabObject != null)
        {
            roleHudPrefab = prefabObject.GetComponent<RoleMechanicHudView>();
        }
#endif

        return roleHudPrefab;
    }

    private void BuildRuntimeHudFallback()
    {
        GameObject canvasGo = new GameObject("RoleMechanicHUD_Runtime", typeof(RectTransform), typeof(Canvas), typeof(CanvasScaler), typeof(GraphicRaycaster));
        canvasGo.transform.SetParent(transform, false);
        hudCanvas = canvasGo.GetComponent<Canvas>();
        hudCanvas.renderMode = RenderMode.ScreenSpaceOverlay;
        hudCanvas.sortingOrder = 250;

        CanvasScaler scaler = canvasGo.GetComponent<CanvasScaler>();
        scaler.uiScaleMode = CanvasScaler.ScaleMode.ScaleWithScreenSize;
        scaler.referenceResolution = new Vector2(1920f, 1080f);
        scaler.matchWidthOrHeight = 0.5f;

        GameObject root = new GameObject("ResourceRoot", typeof(RectTransform), typeof(Image), typeof(HorizontalLayoutGroup));
        root.transform.SetParent(canvasGo.transform, false);
        RectTransform rootRect = root.GetComponent<RectTransform>();
        rootRect.anchorMin = new Vector2(0.5f, 0f);
        rootRect.anchorMax = new Vector2(0.5f, 0f);
        rootRect.pivot = new Vector2(0.5f, 0f);
        rootRect.anchoredPosition = new Vector2(0f, 34f);
        rootRect.sizeDelta = new Vector2(430f, 42f);
        root.GetComponent<Image>().color = new Color(0.06f, 0.04f, 0.025f, 0.72f);

        HorizontalLayoutGroup layout = root.GetComponent<HorizontalLayoutGroup>();
        layout.padding = new RectOffset(12, 12, 7, 7);
        layout.spacing = 10f;
        layout.childAlignment = TextAnchor.MiddleCenter;
        layout.childControlWidth = true;
        layout.childControlHeight = true;
        layout.childForceExpandWidth = false;
        layout.childForceExpandHeight = true;

        labelText = CreateHudText("Label", root.transform, 22f, TextAlignmentOptions.Left);
        LayoutElement labelLayout = labelText.gameObject.AddComponent<LayoutElement>();
        labelLayout.preferredWidth = 160f;

        GameObject bar = new GameObject("Bar", typeof(RectTransform), typeof(Image));
        bar.transform.SetParent(root.transform, false);
        LayoutElement barLayout = bar.AddComponent<LayoutElement>();
        barLayout.preferredWidth = 220f;
        bar.GetComponent<Image>().color = new Color(0.14f, 0.10f, 0.07f, 0.95f);

        GameObject fill = new GameObject("Fill", typeof(RectTransform), typeof(Image));
        fill.transform.SetParent(bar.transform, false);
        RectTransform fillRect = fill.GetComponent<RectTransform>();
        fillRect.anchorMin = Vector2.zero;
        fillRect.anchorMax = Vector2.one;
        fillRect.offsetMin = Vector2.zero;
        fillRect.offsetMax = Vector2.zero;
        fillImage = fill.GetComponent<Image>();
        fillImage.type = Image.Type.Filled;
        fillImage.fillMethod = Image.FillMethod.Horizontal;
        fillImage.color = GetRoleColor();
    }

    private TextMeshProUGUI CreateHudText(string objectName, Transform parent, float size, TextAlignmentOptions alignment)
    {
        GameObject textGo = new GameObject(objectName, typeof(RectTransform), typeof(TextMeshProUGUI));
        textGo.transform.SetParent(parent, false);
        TextMeshProUGUI text = textGo.GetComponent<TextMeshProUGUI>();
        text.fontSize = size;
        text.fontStyle = FontStyles.Bold;
        text.alignment = alignment;
        text.color = Color.white;
        text.raycastTarget = false;
        return text;
    }

    private Color GetRoleColor()
    {
        switch (roleKind)
        {
            case RoleKind.Mage: return new Color(0.28f, 0.72f, 1f, 1f);
            case RoleKind.Swordsman: return new Color(1f, 0.55f, 0.18f, 1f);
            case RoleKind.Engineer: return new Color(0.42f, 0.95f, 0.76f, 1f);
            default: return Color.white;
        }
    }

    private void RefreshHud()
    {
        if (labelText == null && fillImage == null && hudView == null) return;

        string label = "";
        float fillAmount = 0f;
        bool showSwordPips = false;

        switch (roleKind)
        {
            case RoleKind.Mage:
                label = $"\u80fd\u91cf {Mathf.FloorToInt(resource)}/{Mathf.FloorToInt(resourceMax)}";
                fillAmount = resourceMax > 0f ? resource / resourceMax : 0f;
                break;
            case RoleKind.Swordsman:
                int nextSeconds = swordFocusStacks >= swordFocusMaxStacks
                    ? 0
                    : Mathf.Max(0, Mathf.CeilToInt(swordFocusInterval - swordFocusTimer));
                label = swordFocusStacks >= swordFocusMaxStacks
                    ? $"\u5251\u52bf {swordFocusStacks}/{swordFocusMaxStacks}  \u6ee1\u5c42"
                    : $"\u5251\u52bf {swordFocusStacks}/{swordFocusMaxStacks}  {nextSeconds}s";
                fillAmount = swordFocusStacks >= swordFocusMaxStacks
                    ? 1f
                    : Mathf.Clamp01(swordFocusTimer / Mathf.Max(1f, swordFocusInterval));
                showSwordPips = true;
                break;
            case RoleKind.Engineer:
                int threshold = GetEngineerModificationThreshold();
                label = $"\u96f6\u4ef6 {parts}/{threshold}";
                fillAmount = threshold > 0 ? Mathf.Clamp01((float)parts / threshold) : 0f;
                break;
        }

        if (hudView != null)
        {
            hudView.SetValue(label, fillAmount, swordFocusStacks, swordFocusMaxStacks, showSwordPips);
            return;
        }

        if (labelText != null) labelText.text = label;
        if (fillImage != null) fillImage.fillAmount = fillAmount;
    }

    private void RefreshHudLegacy()
    {
        if (labelText == null || fillImage == null) return;

        switch (roleKind)
        {
            case RoleKind.Mage:
                labelText.text = $"能量 {Mathf.FloorToInt(resource)}/{Mathf.FloorToInt(resourceMax)}";
                fillImage.fillAmount = resource / resourceMax;
                break;
            case RoleKind.Swordsman:
                int nextSeconds = swordFocusStacks >= swordFocusMaxStacks
                    ? 0
                    : Mathf.Max(0, Mathf.CeilToInt(swordFocusInterval - swordFocusTimer));
                labelText.text = swordFocusStacks >= swordFocusMaxStacks
                    ? $"剑势 {swordFocusStacks}/{swordFocusMaxStacks}  满层"
                    : $"剑势 {swordFocusStacks}/{swordFocusMaxStacks}  {nextSeconds}s";
                fillImage.fillAmount = swordFocusStacks >= swordFocusMaxStacks
                    ? 1f
                    : Mathf.Clamp01(swordFocusTimer / Mathf.Max(1f, swordFocusInterval));
                break;
            case RoleKind.Engineer:
                int threshold = GetEngineerModificationThreshold();
                labelText.text = $"零件 {parts}/{threshold}";
                fillImage.fillAmount = threshold > 0 ? Mathf.Clamp01((float)parts / threshold) : 0f;
                break;
        }
    }
}
