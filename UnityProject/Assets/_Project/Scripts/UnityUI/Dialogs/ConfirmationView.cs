// 说明：确认弹层只回报确认或取消，危险行为仍由 Presenter 发出命令。
using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using Zhijian.Presentation.Navigation;
namespace Zhijian.UnityUI.Dialogs
{
    public sealed class ConfirmationView : MonoBehaviour
    {
        [SerializeField] private TMP_Text title, body, confirmLabel, cancelLabel;
        [SerializeField] private Button confirm, cancel;
        public void Bind(Confirmation data, Action<bool> complete)
        { title.text=data.Title; body.text=data.Body; confirmLabel.text=data.ConfirmText; cancelLabel.text=data.CancelText;
            confirm.onClick.AddListener(() => complete(true)); cancel.onClick.AddListener(() => complete(false)); }
    }
}
