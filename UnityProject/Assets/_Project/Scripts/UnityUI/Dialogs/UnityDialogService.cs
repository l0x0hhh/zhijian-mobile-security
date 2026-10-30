// 说明：统一拥有确认弹层的生命周期，返回键关闭最上层，重复回调只执行一次。
using System;
using System.Collections.Generic;
using UnityEngine;
using Zhijian.Presentation.Navigation;
namespace Zhijian.UnityUI.Dialogs
{
    public sealed class UnityDialogService : IConfirmationService, IDisposable
    {
        private readonly ConfirmationView prefab;
        private readonly Transform host;
        private readonly Action<GameObject> applyTheme;
        private readonly List<Lease> stack=new List<Lease>();
        public UnityDialogService(ConfirmationView prefab, Transform host, Action<GameObject> applyTheme)
        { this.prefab=prefab; this.host=host; this.applyTheme=applyTheme; }
        public IDisposable Show(Confirmation data, Action<bool> completed)
        {
            if (stack.Count > 0) stack[stack.Count-1].Close(false);
            var view=UnityEngine.Object.Instantiate(prefab, host, false);
            view.transform.SetAsLastSibling();
            var lease=new Lease(this, view, completed); stack.Add(lease);
            view.Bind(data, lease.Close); applyTheme(view.gameObject); return lease;
        }
        public bool CloseTop() { if (stack.Count == 0) return false; stack[stack.Count-1].Close(false); return true; }
        public void Dispose() { while (stack.Count > 0) stack[stack.Count-1].Dispose(); }
        private sealed class Lease : IDisposable
        {
            private UnityDialogService owner; private ConfirmationView view; private Action<bool> complete;
            public Lease(UnityDialogService owner, ConfirmationView view, Action<bool> complete) { this.owner=owner; this.view=view; this.complete=complete; }
            public void Close(bool accepted)
            {
                var callback=complete; if (owner == null) return;
                Dispose(); callback?.Invoke(accepted);
            }
            public void Dispose()
            {
                if (owner == null) return;
                owner.stack.Remove(this); owner=null; complete=null;
                if (view != null) { view.gameObject.SetActive(false); UnityEngine.Object.Destroy(view.gameObject); view=null; }
            }
        }
    }
}
