using System;
using System.Collections.Generic;
using System.Linq;
using Rollgeon.Attributes;
using Rollgeon.Attributes.Modifiers;
using Sirenix.OdinInspector;

namespace Rollgeon.Items
{
    [Serializable, HideReferenceObjectPicker]
    public class PersistentModifierDef
    {
        [ValueDropdown(nameof(GetStatTypes))]
        public Type TargetStat;
        public ModifierOperation Operation;
        public float Amount;
        public ModifierDirection Direction = ModifierDirection.Intrinsic;

#if UNITY_EDITOR
        private static IEnumerable<ValueDropdownItem<Type>> GetStatTypes()
        {
            return AppDomain.CurrentDomain.GetAssemblies()
                .SelectMany(a => { try { return a.GetTypes(); } catch { return Type.EmptyTypes; } })
                .Where(t => typeof(IModifiable).IsAssignableFrom(t) && !t.IsInterface && !t.IsAbstract)
                .Select(t => new ValueDropdownItem<Type>(t.Name, t));
        }
#endif
    }
}
