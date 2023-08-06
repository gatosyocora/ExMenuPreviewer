using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEngine;
using VRC.SDK3.Avatars.Components;
using VRC.SDK3.Avatars.ScriptableObjects;

public class ExMenuPreviewer : MonoBehaviour
{
    [SerializeField]
    public VRCAvatarDescriptor avatarDescriptor;

    [HideInInspector]
    public bool isPreview = false;

    public void Start()
    {
        if (isPreview)
        {
            var animatorController = avatarDescriptor.baseAnimationLayers.First(x => x.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
            var animator = transform.GetComponent<Animator>();

            SetAnimatorController(animator, avatarDescriptor);
            SetDefaultParameters(animator, avatarDescriptor);
        }
    }

    public void Update()
    {
        if (isPreview)
        {
            EditorUtility.SetDirty(this);
        }
    }

    public void Reset()
    {
        if (avatarDescriptor == null)
        {
            avatarDescriptor = transform.GetComponent<VRCAvatarDescriptor>();
        }
    }

    public void SetAnimatorController(Animator animator, VRCAvatarDescriptor avatarDescriptor)
    {
        var animatorController = avatarDescriptor.baseAnimationLayers.First(x => x.type == VRCAvatarDescriptor.AnimLayerType.FX).animatorController;
        animator.runtimeAnimatorController = animatorController;
    }

    public void SetDefaultParameters(Animator animator, VRCAvatarDescriptor avatarDescriptor)
    {
        var parameters = avatarDescriptor.expressionParameters.parameters;

        foreach (var parameter in parameters)
        {
            switch (parameter.valueType)
            {
                case VRCExpressionParameters.ValueType.Bool:
                    animator.SetBool(parameter.name, parameter.defaultValue == 1);
                    break;
                case VRCExpressionParameters.ValueType.Float:
                    animator.SetFloat(parameter.name, parameter.defaultValue);
                    break;
                case VRCExpressionParameters.ValueType.Int:
                    animator.SetInteger(parameter.name, (int)parameter.defaultValue);
                    break;
                default:
                    break;
            }
        }
    }

    [CustomEditor(typeof(ExMenuPreviewer))]
    public class ExMenuPreviewerEditor : Editor
    {
        private bool useRadicalPuppet = false;
        private string radicalPuppetParamName = null;
        private float radicalPuppetValue;
        private List<VRCExpressionsMenu> stackExMenus = new List<VRCExpressionsMenu>();

        private Texture2D subMenuIcon, quickActionsIcon, backIcon, playIcon, radialIcon, toggleActiveIcon, noIcon, backHomeIcon;

        private GUIStyle centeredLabelStyle;

        private void OnEnable()
        {
            subMenuIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("f38c50661aaa99e4387abc4ad07b1b11")); // submenu
            quickActionsIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("46dd9389c8300254a8e8e0744d5b98a2")); // quick_actions
            backIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("006c5e775f471e24d95601870f1595eb")); // back
            playIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("4a6e26afd64e4054c803edbcd61c88b1")); // play
            toggleActiveIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("1ce069730c704a14d97f565ad9346232")); // toggle_active
            radialIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("74f41a30862bcf14bbdf201500e2cc13")); // radial
            noIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("1d64a6841faa85a45b34bae80d538260")); // no_icon
            backHomeIcon = AssetDatabase.LoadAssetAtPath<Texture2D>(AssetDatabase.GUIDToAssetPath("a0fe4b610cc5c224195276d6d536a7aa")); // back_home
        }

        public override void OnInspectorGUI()
        {
            var previewer = target as ExMenuPreviewer;
            var animator = previewer.transform.GetComponent<Animator>();

            base.OnInspectorGUI();

            EditorGUILayout.Space();

            var label = !previewer.isPreview ? "プレビュー有効化" : "プレビュー無効化";
            if (GUILayout.Button(label))
            {
                previewer.isPreview = !previewer.isPreview;

                if (EditorApplication.isPlaying && previewer.isPreview)
                {
                    previewer.SetAnimatorController(animator, previewer.avatarDescriptor);
                    previewer.SetDefaultParameters(animator, previewer.avatarDescriptor);
                }
            }

            if (previewer.isPreview && !EditorApplication.isPlaying)
            {
                EditorGUILayout.HelpBox("プレビュー有効中です(Unityで再生モード中のみ表示されます)", MessageType.Info);
            }

            if (!previewer.isPreview || !EditorApplication.isPlaying) return;

            var size = 400;
            var rect = GUILayoutUtility.GetRect(size, size, size, size);

            centeredLabelStyle = GUI.skin.label;
            centeredLabelStyle.alignment = TextAnchor.UpperCenter;

            MenuItem[] items;
            if (stackExMenus.Count <= 0)
            {
                var controls = previewer.avatarDescriptor.expressionsMenu.controls.ToArray();

                items = new MenuItem[]
                {
                    new MenuItem("Back", backHomeIcon, null, "", 0, null, ItemType.Back),
                    new MenuItem("Quick Actions", quickActionsIcon, null, "", 0, null, ItemType.Other)
                }
                .Concat(controls.Select(x => new MenuItem(x.name, x.icon, x.subMenu, x.parameter.name, x.value, x.subParameters, ConvertToItemType(x.type))))
                .ToArray();
            }
            else
            {
                var controls = stackExMenus.Last().controls;

                items = new MenuItem[]
                {
                    new MenuItem("Back", backIcon, null, "", 0, null, ItemType.Back)
                }
                .Concat(controls.Select(x => new MenuItem(x.name, x.icon, x.subMenu, x.parameter.name, x.value, x.subParameters, ConvertToItemType(x.type))))
                .ToArray();
            }

            HandleScope(() =>
            {
                var center = new Vector3(rect.x + size / 2, rect.y + size / 2, 0);
                var radius = size / 2;
                var iconSize = size / 5;
                var labelHeight = 20;
                var angle = 360f / items.Length;

                Handles.color = new Color(0, 0.2f, 0.2f);
                Handles.DrawSolidDisc(center, Vector3.forward, radius);

                Handles.color = new Color(0, 0.3f, 0.3f);
                Handles.DrawWireArc(center, Vector3.forward, Vector3.down, 360, radius);

                // 選択中の項目を描画と選択処理
                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];

                    var previousPoint = center + Quaternion.Euler(0, 0, -angle / 2 + angle * i) * Vector3.down * radius;
                    var point = center + Quaternion.Euler(0, 0, -angle / 2 + angle * (i + 1)) * Vector3.down * radius;

                    if (ContainsTriangle(Event.current.mousePosition, center, previousPoint, point))
                    {
                        Handles.DrawSolidArc(center, Vector3.forward, previousPoint - center, angle, radius);

                        if (Event.current.type == EventType.MouseDown)
                        {
                            OnClickMenuItem(item, previewer, animator);
                        }
                    }
                }

                // アイコンと境界線を描画
                for (var i = 0; i < items.Length; i++)
                {
                    var item = items[i];
                    var point = center + Quaternion.Euler(0, 0, -angle / 2 + angle * (i + 1)) * Vector3.down * radius;
                    Handles.DrawLine(center, point);

                    Texture2D icon = item.icon != null ? item.icon : noIcon;

                    var iconCenter = (center + Quaternion.Euler(0, 0, -angle / 2 + angle / 2 * (2 * i + 1)) * Vector3.down * radius / 1.5f) - Vector3.one * iconSize / 2;
                    GUI.DrawTexture(new Rect(iconCenter.x, iconCenter.y, iconSize, iconSize), icon);
                    GUI.Label(new Rect(iconCenter.x, iconCenter.y + iconSize, iconSize, labelHeight), item.name, centeredLabelStyle);

                    Texture2D miniIcon = GetMiniIcon(item);
                    if (miniIcon != null)
                    {
                        GUI.DrawTexture(new Rect(iconCenter.x + iconSize * 0.8f, iconCenter.y + iconSize * 0.8f, iconSize / 3, iconSize / 3), miniIcon);
                    }
                }

                // 有効中アイコンを描画
                if (EditorApplication.isPlaying)
                {
                    for (int i = 0; i < items.Length; i++)
                    {
                        var item = items[i];

                        var parameter = previewer.avatarDescriptor.expressionParameters.parameters.FirstOrDefault(x => x.name == item.parameter);

                        if (parameter != null && IsActiveItem(parameter.valueType, animator, item))
                        {
                            var iconCenter = (center + Quaternion.Euler(0, 0, -angle / 2 + angle / 2 * (2 * i + 1)) * Vector3.down * radius / 1.5f) - Vector3.one * iconSize / 2;
                            Debug.Log("inner");
                            GUI.DrawTexture(new Rect(iconCenter.x, iconCenter.y, iconSize, iconSize), toggleActiveIcon);
                        }
                    }
                }
            });

            if (useRadicalPuppet)
            {
                using (var check = new EditorGUI.ChangeCheckScope())
                {
                    radicalPuppetValue = EditorGUILayout.Slider(radicalPuppetParamName, radicalPuppetValue, 0f, 1f);

                    if (check.changed)
                    {
                        animator.SetFloat(radicalPuppetParamName, radicalPuppetValue);
                    }
                }
            }
        }

        private void HandleScope(Action action)
        {
            using (new EditorGUILayout.VerticalScope())
            {
                Handles.BeginGUI();
                var defaultColor = Handles.color;
                action();
                Handles.color = defaultColor;
                Handles.EndGUI();
            }
        }

        private bool ContainsTriangle(Vector2 target, Vector2 p1, Vector2 p2, Vector2 p3)
        {
            var b1 = Sign(target, p1, p2) < 0;
            var b2 = Sign(target, p2, p3) < 0;
            var b3 = Sign(target, p3, p1) < 0;

            return (b1 == b2) && (b2 == b3);
        }

        private float Sign(Vector2 p1, Vector2 p2, Vector2 p3)
        {
            return (p1.x - p3.x) * (p2.y - p3.y) - (p2.x - p3.x) * (p1.y - p3.y);
        }

        private ItemType ConvertToItemType(VRCExpressionsMenu.Control.ControlType controlType)
        {
            switch (controlType)
            {
                case VRCExpressionsMenu.Control.ControlType.Toggle:
                    return ItemType.Toggle;
                case VRCExpressionsMenu.Control.ControlType.SubMenu:
                    return ItemType.SubMenu;
                case VRCExpressionsMenu.Control.ControlType.Button:
                    return ItemType.Button;
                case VRCExpressionsMenu.Control.ControlType.RadialPuppet:
                    return ItemType.RadialPuppet;
                default:
                    return ItemType.Other;
            }
        }

        private Texture2D GetMiniIcon(MenuItem item)
        {
            switch (item.itemType)
            {
                case ItemType.SubMenu:
                    return subMenuIcon;
                case ItemType.Toggle:
                    return playIcon;
                case ItemType.RadialPuppet:
                    return radialIcon;
                default:
                    return null;
            }
        }

        private bool IsActiveItem(VRCExpressionParameters.ValueType parameterValueType, Animator animator, MenuItem item)
        {
            switch (parameterValueType) {
                case VRCExpressionParameters.ValueType.Int:
                    return animator.GetInteger(item.parameter) == (int)item.value;
                case VRCExpressionParameters.ValueType.Bool:
                    return animator.GetBool(item.parameter) == (item.value == 1);
                case VRCExpressionParameters.ValueType.Float:
                    return animator.GetFloat(item.parameter) == item.value;
                default:
                    return false;
            }
        }

        private void UpdateAnimatorParameterValue(VRCExpressionParameters.Parameter parameter, Animator animator, MenuItem item)
        {
            switch (parameter.valueType)
            {
                case VRCExpressionParameters.ValueType.Int:
                    if (animator.GetInteger(item.parameter) != (int)item.value)
                    {
                        animator.SetInteger(item.parameter, (int)item.value);
                    }
                    else
                    {
                        animator.SetInteger(item.parameter, (int)parameter.defaultValue);
                    }
                    break;
                case VRCExpressionParameters.ValueType.Bool:
                    if (animator.GetBool(item.parameter) != (item.value == 1))
                    {
                        animator.SetBool(item.parameter, item.value == 1);
                    }
                    else
                    {
                        animator.SetBool(item.parameter, item.value != 1);
                    }
                    break;
                case VRCExpressionParameters.ValueType.Float:
                    if (animator.GetFloat(item.parameter) != item.value)
                    {
                        animator.SetFloat(item.parameter, item.value);
                    }
                    else
                    {
                        animator.SetFloat(item.parameter, parameter.defaultValue);
                    }
                    break;
            }
        }

        private void OnClickMenuItem(MenuItem item, ExMenuPreviewer previewer, Animator animator)
        {
            if (item.itemType != ItemType.RadialPuppet)
            {
                useRadicalPuppet = false;
            }

            switch (item.itemType)
            {
                case ItemType.SubMenu:
                    stackExMenus.Add(item.subMenu);
                    Repaint();
                    break;
                case ItemType.Back:
                    if (stackExMenus.Count > 0)
                    {
                        stackExMenus.RemoveAt(stackExMenus.Count - 1);
                    }
                    Repaint();
                    break;
                case ItemType.Button:
                case ItemType.Toggle:
                    if (!EditorApplication.isPlaying) break;
                    var parameter = previewer.avatarDescriptor.expressionParameters.parameters.FirstOrDefault(x => x.name == item.parameter);
                    if (parameter == null) break;

                    UpdateAnimatorParameterValue(parameter, animator, item);
                    break;
                case ItemType.RadialPuppet:
                    useRadicalPuppet = !useRadicalPuppet;

                    if (useRadicalPuppet)
                    {
                        var name = item.subParameters[0].name;
                        radicalPuppetParamName = name;
                        radicalPuppetValue = animator.GetFloat(name);
                    }
                    break;
                default:
                    break;
            }
        }

        public class MenuItem
        {
            public string name;
            public Texture2D icon;
            public VRCExpressionsMenu subMenu;
            public string parameter;
            public float value;
            public VRCExpressionsMenu.Control.Parameter[] subParameters;
            public ItemType itemType;

            public MenuItem(string name, Texture2D icon, VRCExpressionsMenu subMenu, string parameter, float value, VRCExpressionsMenu.Control.Parameter[] subParameters, ItemType itemType)
            {
                this.name = name;
                this.icon = icon;
                this.subMenu = subMenu;
                this.parameter = parameter;
                this.value = value;
                this.subParameters = subParameters;
                this.itemType = itemType;
            }
        }

        public enum ItemType
        {
            Toggle,
            Button,
            RadialPuppet,
            SubMenu,
            Back,
            Other
        }
    }
}
