using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Events;
using UnityEngine.UI;

namespace JojoP.AOT.UI
{
    /// <summary>UI 行为基类：生命周期 + 常用控件监听。</summary>
    public abstract class UIBehaviour : MonoBehaviour
    {
        readonly List<Button> _buttons = new();
        readonly List<Toggle> _toggles = new();
        readonly List<InputField> _inputFields = new();

        bool _ready;

        public Type Type => GetType();

        /// <summary>场景挂载或 UIManager 打开时调用；幂等。</summary>
        public void EnsureReady()
        {
            if (_ready) return;
            InternalRegister();
            InternalInit();
            _ready = true;
        }

        internal void InternalRegister() => OnRegister();
        internal void InternalInit() => OnInit();
        internal void InternalOpen() { OnOpenEvent(); OnOpen(); }
        internal void InternalClose() { OnCloseEvent(); OnClose(); }

        protected void AddBtnClickListener(Button btn, UnityAction action)
        {
            if (btn == null || _buttons.Contains(btn)) return;
            _buttons.Add(btn);
            btn.onClick.RemoveAllListeners();
            btn.onClick.AddListener(action);
        }

        protected void AddToggleClickListener(Toggle toggle, UnityAction<bool, Toggle> action)
        {
            if (toggle == null || _toggles.Contains(toggle)) return;
            _toggles.Add(toggle);
            toggle.onValueChanged.RemoveAllListeners();
            toggle.onValueChanged.AddListener(isOn => action?.Invoke(isOn, toggle));
        }

        protected void AddInputFieldListener(InputField input, UnityAction<string> change, UnityAction<string> end)
        {
            if (input == null || _inputFields.Contains(input)) return;
            _inputFields.Add(input);
            input.onValueChanged.RemoveAllListeners();
            input.onEndEdit.RemoveAllListeners();
            if (change != null) input.onValueChanged.AddListener(change);
            if (end != null) input.onEndEdit.AddListener(end);
        }

        protected abstract void OnRegister();
        protected virtual void OnInit() { }
        protected virtual void OnOpenEvent() { }
        protected virtual void OnOpen() { }
        protected virtual void OnCloseEvent() { }
        protected virtual void OnClose() { }

        protected virtual void OnDestroy()
        {
            foreach (var b in _buttons)
            {
                if (b != null) b.onClick.RemoveAllListeners();
            }
            _buttons.Clear();

            foreach (var t in _toggles)
            {
                if (t != null) t.onValueChanged.RemoveAllListeners();
            }
            _toggles.Clear();

            foreach (var i in _inputFields)
            {
                if (i == null) continue;
                i.onValueChanged.RemoveAllListeners();
                i.onEndEdit.RemoveAllListeners();
            }
            _inputFields.Clear();
        }
    }
}
