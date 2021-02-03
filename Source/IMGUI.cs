using System.Collections.Generic;
using System.Linq;
using Apos.Input;

namespace Apos.Gui {
    // TODO: Make IMGUI implement IParent?
    public class IMGUI {
        public void UpdateSetup() {
            // 1. Cleanup last cycle
            // 2. Pending components become active.
            //      a. Set parenting.
            //      b. No parent means root parent.
            // 3. Update pref sizes.
            // 4. Apply pref sizes.
            // 5. Update setup.
            Cleanup();
            while (PendingComponents.Count > 0) {
                var pc = PendingComponents.Dequeue();
                ActiveComponents.Add(pc.Name, pc.Component);
                if (pc.Parent != null) {
                    pc.Parent.Add(pc.Component);
                } else {
                    Roots.Add(pc.Component);
                }
            }
            if (Focus == null) {
                FindNextFocus();
            }

            // TODO: Process pending action queue. (It doesn't exist yet.)

            foreach (var c in Roots) {
                c.UpdatePrefSize();
                // TODO: Update position?
                c.Width = c.PrefWidth;
                c.Height = c.PrefHeight;
                c.UpdateSetup();
            }
        }
        public void UpdateInput() {
            foreach (var c in Roots)
                c.UpdateInput();

            // TODO: Need to handle the whole lifecycle of FocusPrev and FocusNext, same as buttons. (Pressed, HeldOnly, Released)
            if (Default.FocusPrev.Released()) {
                FindPrevFocus();
            }
            if (Default.FocusNext.Released()) {
                FindNextFocus();
            }
        }
        public void Update() {
            foreach (var c in Roots)
                c.Update();
        }
        public void UpdateAll() {
            UpdateSetup();
            UpdateInput();
            Update();
        }
        public void Draw() {
            foreach (var c in Roots)
                c.Draw();
        }

        public void Push(IParent p, int maxChildren = 0) {
            if (CurrentParent != null) {
                Parents.Push((CurrentParent, MaxChildren, ChildrenCount));
            }
            CurrentParent = p;
            MaxChildren = maxChildren;
            ChildrenCount = 0;
        }
        public void Pop() {
            if (Parents.Count > 0) {
                var pop = Parents.Pop();
                CurrentParent = pop.Parent;
                MaxChildren = pop.MaxChildren;
                ChildrenCount = pop.ChildrenCount;
            } else {
                CurrentParent = null;
                MaxChildren = 0;
                ChildrenCount = 0;
            }
        }
        public void Add(string name, IComponent c) {
            // NOTE: This should only be called if the component hasn't already been added.
            PendingComponents.Enqueue((name, CurrentParent, c));

            CountChild();
        }
        public bool TryGetValue(string name, out IComponent c) {
            if (ActiveComponents.TryGetValue(name, out c)) {
                CountChild();
                return true;
            }

            foreach (var pc in PendingComponents) {
                if (pc.Name == name) {
                    c = pc.Component;
                    CountChild();
                    return true;
                }
            }

            return false;
        }
        public int NextId() {
            return _lastId++;
        }
        private void CountChild() {
            ChildrenCount++;

            if (CurrentParent != null && MaxChildren > 0 && ChildrenCount >= MaxChildren) {
                Pop();
            }
        }
        private void Cleanup() {
            _lastId = 0;
            foreach (var kc in ActiveComponents.Reverse()) {
                if (kc.Value.LastPing != InputHelper.CurrentFrame - 1) {
                    Remove(kc.Key, kc.Value);
                }
            }
            CurrentParent = null;
            MaxChildren = 0;
            ChildrenCount = 0;
            Parents.Clear();
        }
        private void Remove(string name, IComponent c) {
            if (_focus == name) {
                FindPrevFocus();
            }

            ActiveComponents.Remove(name);
            if (c.Parent != null) {
                c.Parent.Remove(c);
            } else {
                Roots.Remove(c);
            }
            // TODO: Remove from PendingComponents? Probably not since that case can't happen?
        }
        private void FindPrevFocus() {
            string? initialFocus = null;
            if (Focus != null) {
                initialFocus = Focus;
            } else if (Roots.Count > 0) {
                initialFocus = Roots.First().Name;
            }
            if (initialFocus != null) {
                string newFocus = initialFocus;
                do {
                    var c = ActiveComponents[newFocus].GetPrev();
                    newFocus = c.Name;
                    if (c.IsFocusable) {
                        Focus = newFocus;
                        break;
                    }
                } while (initialFocus != newFocus);
            }
        }
        private void FindNextFocus() {
            string? initialFocus = null;
            if (Focus != null) {
                initialFocus = Focus;
            } else if (Roots.Count > 0) {
                initialFocus = Roots.First().Name;
            }
            if (initialFocus != null) {
                string newFocus = initialFocus;
                do {
                    var c = ActiveComponents[newFocus].GetNext();
                    newFocus = c.Name;
                    if (c.IsFocusable) {
                        Focus = newFocus;
                        break;
                    }
                } while (initialFocus != newFocus);
            }
        }

        public IParent? CurrentParent;
        public int MaxChildren = 0;
        public int ChildrenCount = 0;

        private Stack<(IParent Parent, int MaxChildren, int ChildrenCount)> Parents = new Stack<(IParent, int, int)>();
        private List<IComponent> Roots = new List<IComponent>();
        private Dictionary<string, IComponent> ActiveComponents = new Dictionary<string, IComponent>();
        private Queue<(string Name, IParent? Parent, IComponent Component)> PendingComponents = new Queue<(string, IParent?, IComponent)>();
        private int _lastId = 0;
        private string? Focus {
            get => _focus;
            set {
                if (_focus != null) {
                    ActiveComponents[_focus].IsFocused = false;
                }
                _focus = value;
                if (_focus != null) {
                    ActiveComponents[_focus].IsFocused = true;
                }
            }
        }
        private string? _focus;
    }
}
