using System.Collections.Generic;
using System.Text;
using Patterns;
using Rollgeon.Combat.Rolls;
using Rollgeon.Items;
using Rollgeon.Upgrades.Dice;

namespace Rollgeon.Editor.Tools.Item
{
    /// <summary>
    /// Los disparadores que un ítem pasivo puede usar, con nombre de diseño.
    /// </summary>
    /// <remarks>
    /// <para>
    /// El "cuándo" era lo único que un diseñador nombró como bloqueante de la tool vieja, y la
    /// causa está a la vista: <see cref="PassiveItemHook.TriggerEvent"/> es un
    /// <see cref="EventName"/>, o sea el bus entero del juego — más de cien entradas, de las que
    /// sirven ~una docena. Las buenas estaban listadas <b>como texto</b> en un InfoBox que había
    /// que leer y después buscar a mano en el desplegable, y elegir mal no da error: el ítem
    /// simplemente nunca dispara. La única salida era preguntarle a un programador.
    /// </para>
    /// <para>
    /// Esto es sólo editor y no inventa formato: el asset sigue guardando
    /// <see cref="PassiveItemHook.Kind"/> + <see cref="PassiveItemHook.TriggerEvent"/> +
    /// <see cref="PassiveItemHook.Subject"/> exactamente como antes. Lo que agrega es un nombre
    /// legible y una lista cerrada, para que lo que se ofrece sea lo que funciona.
    /// </para>
    /// <para>
    /// <b>No están, y no por olvido:</b> <c>OnRunStart</c> lleva en <c>args[0]</c> el id de la
    /// run, no el del jugador, así que el filtro del hook lo descarta siempre; y
    /// <c>OnComboCrossed</c> se dispara con <c>Guid.Empty</c>. Ambos se ven perfectamente
    /// elegibles en el desplegable crudo y no disparan nunca. <c>OnCombatStart</c> (mismo
    /// problema: args[0] es la sala) SÍ está, pero declarado con
    /// <see cref="PassiveHookSubject.None"/>, que desactiva el filtro de entidad.
    /// </para>
    /// <para>
    /// <b>Al sumar una entrada</b>, confirmá contra el <c>EventManager.Trigger</c> real qué lleva
    /// <c>args[0]</c>: el hook filtra por ahí (o por <c>args[1]</c> con
    /// <see cref="PassiveHookSubject.Target"/>), y un evento cuyo primer argumento no es un
    /// <c>Guid</c> dispara para cualquiera. Eso se declara con
    /// <see cref="TriggerOption.FiltersByEntity"/>, no se deja como sorpresa.
    /// </para>
    /// </remarks>
    public static class ItemTriggerCatalog
    {
        /// <summary>Un disparador ofrecible, y a qué campos del hook se traduce.</summary>
        public readonly struct TriggerOption
        {
            /// <summary>Clave estable. No se muestra: sirve para tests y para el popup.</summary>
            public string Id { get; }

            /// <summary>Cómo lo lee el diseñador. Va en la lista y en la frase.</summary>
            public string DisplayName { get; }

            /// <summary>Una línea de qué significa exactamente, incluidas las trampas.</summary>
            public string Help { get; }

            public PassiveHookKind Kind { get; }

            /// <summary>Sólo con <see cref="PassiveHookKind.EventBus"/>.</summary>
            public EventName Event { get; }

            public PassiveHookSubject Subject { get; }

            /// <summary>
            /// <c>false</c> si el evento no lleva un <c>Guid</c> en la posición del sujeto: el hook
            /// no tiene contra quién comparar y dispara siempre.
            /// </summary>
            public bool FiltersByEntity { get; }

            /// <summary>Sólo con <see cref="PassiveHookKind.ComboPlayed"/>: pide elegir combos.</summary>
            public bool UsesComboIds { get; }

            internal TriggerOption(
                string id, string displayName, string help,
                PassiveHookKind kind, EventName evt,
                PassiveHookSubject subject = PassiveHookSubject.Source,
                bool filtersByEntity = true, bool usesComboIds = false)
            {
                Id = id;
                DisplayName = displayName;
                Help = help;
                Kind = kind;
                Event = evt;
                Subject = subject;
                FiltersByEntity = filtersByEntity;
                UsesComboIds = usesComboIds;
            }
        }

        static TriggerOption Bus(
            string id, string name, string help, EventName evt,
            PassiveHookSubject subject = PassiveHookSubject.Source, bool filtersByEntity = true) =>
            new TriggerOption(id, name, help, PassiveHookKind.EventBus, evt, subject, filtersByEntity);

        static TriggerOption Combo(string id, string name, string help, bool usesComboIds) =>
            new TriggerOption(id, name, help, PassiveHookKind.ComboPlayed, default,
                              PassiveHookSubject.Source, true, usesComboIds);

        /// <summary>
        /// Los disparadores ofrecidos, en orden de uso esperado: primero los de combo, que son la
        /// mecánica central, después el turno, la tirada, el daño y los recursos.
        /// </summary>
        public static readonly IReadOnlyList<TriggerOption> All = new[]
        {
            // --- Combo ---------------------------------------------------------
            Combo("combo.any", "Cuando jugás cualquier combo",
                  "Dispara con cualquier combo confirmado, antes de que se aplique el daño.",
                  usesComboIds: false),
            Combo("combo.ids", "Cuando jugás un combo específico",
                  "Dispara sólo con los combos que elijas, antes de que se aplique el daño.",
                  usesComboIds: true),

            // --- Combate -------------------------------------------------------
            Bus("combat.start", "Cuando entrás a un combate",
                "Al iniciarse el combate de la sala. El evento lleva la sala, no al jugador, " +
                "así que no filtra por entidad — el combate siempre es del jugador.",
                EventName.OnCombatStart, PassiveHookSubject.None, filtersByEntity: false),

            // --- Turno ---------------------------------------------------------
            Bus("turn.start", "Cuando empieza tu turno",
                "Al arrancar el turno del jugador.", EventName.OnTurnStarted),
            Bus("turn.end", "Cuando termina tu turno",
                "Al cerrar el turno del jugador.", EventName.OnTurnFinished),

            // --- Tirada --------------------------------------------------------
            Bus("roll.start", "Cuando empezás a tirar",
                "Antes de que salgan los dados.", EventName.OnRollStarted),
            Bus("roll.dice", "Cuando salen los dados",
                "Resultado crudo, ANTES de los rerolls.", EventName.OnDiceRolled),
            Bus("roll.resolved", "Cuando la tirada queda firme",
                "Después de todos los rerolls — es el resultado con el que se juega.",
                EventName.OnRollResolved),

            // --- Daño ----------------------------------------------------------
            Bus("damage.dealt.raw", "Cuando pegás (daño base)",
                "Antes del multiplicador de debilidad y del escudo del enemigo.",
                EventName.OnDamageOutgoing),
            Bus("damage.dealt.final", "Cuando pegás (daño final)",
                "Después de la debilidad, antes de que el escudo lo absorba.",
                EventName.OnDamageIncoming),
            Bus("damage.taken", "Cuando te pegan",
                "El jugador es quien recibe el golpe, antes de que su escudo lo absorba.",
                EventName.OnDamageIncoming, PassiveHookSubject.Target),
            Bus("weakness.hit", "Cuando pegás a una debilidad",
                "El golpe acertó la debilidad del enemigo.", EventName.OnWeaknessHit),

            // --- Recursos ------------------------------------------------------
            Bus("shield.changed", "Cuando cambia tu escudo",
                "Sube o baja.", EventName.OnShieldChanged),
            Bus("gold.changed", "Cuando cambia tu oro",
                "El evento no lleva a quién le cambió, así que no filtra por entidad — para el " +
                "oro da igual, es del jugador.",
                EventName.OnGoldChanged, PassiveHookSubject.Source, filtersByEntity: false),

            // --- Inventario ----------------------------------------------------
            Bus("item.obtained", "Cuando conseguís un ítem",
                "Cubre compras en la tienda y recompensas de cofre.", EventName.OnItemObtained),
            Bus("modifier.added", "Cuando te aplican un modificador",
                "Cualquier buff o debuff que entre sobre un atributo del jugador.",
                EventName.OnModifierAdded),
        };

        /// <summary>
        /// El hook no usa su evento: rinde mientras el ítem esté en el inventario.
        /// </summary>
        /// <remarks>
        /// Los <see cref="PassiveItemHook.PersistentModifiers"/> los aplica
        /// <c>InventoryService.ApplyPersistentModifiers</c> al entrar el ítem, recorriendo los hooks
        /// <b>sin mirar el evento</b>. O sea que en un hook que sólo lleva modificadores el
        /// <c>TriggerEvent</c> es decorativo — Botas Ligeras y Coraza Reforzada están en
        /// <c>OnRunStart</c>, que nunca matchea al jugador, y funcionan igual. Es una tercera
        /// categoría de "cuándo" que no es un evento, y decirlo así evita que alguien "arregle" un
        /// ítem que anda.
        /// </remarks>
        public static bool IsPermanent(PassiveItemHook hook)
        {
            if (hook == null) return false;
            bool hasEffects = hook.Effect?.Effects != null && hook.Effect.Effects.Count > 0;
            bool hasModifiers = hook.PersistentModifiers != null && hook.PersistentModifiers.Count > 0;
            return !hasEffects && hasModifiers;
        }

        /// <summary>
        /// La opción del catálogo que corresponde a <paramref name="hook"/>, o <c>null</c> si
        /// quedó apuntando a algo que ningún ítem puede escuchar.
        /// </summary>
        /// <remarks>
        /// El <c>null</c> es el que alimenta la salud del catálogo: un hook fuera de la lista no da
        /// error en ningún lado, simplemente no dispara nunca, y así se descubre recién jugando.
        /// </remarks>
        public static TriggerOption? Match(PassiveItemHook hook)
        {
            if (hook == null) return null;

            foreach (var option in All)
            {
                if (option.Kind != hook.Kind) continue;

                if (option.Kind == PassiveHookKind.ComboPlayed)
                {
                    bool wantsIds = hook.ComboFilter != null
                                    && hook.ComboFilter.Mode == ComboFilterMode.ComboIds;
                    if (option.UsesComboIds != wantsIds) continue;
                    return option;
                }

                if (option.Event != hook.TriggerEvent) continue;
                if (option.Subject != hook.Subject) continue;
                return option;
            }

            return null;
        }

        /// <summary>Escribe <paramref name="option"/> sobre <paramref name="hook"/>. No hace Undo ni Dirty: eso es del llamador.</summary>
        public static void Apply(PassiveItemHook hook, TriggerOption option)
        {
            if (hook == null) return;

            hook.Kind = option.Kind;

            if (option.Kind == PassiveHookKind.ComboPlayed)
            {
                hook.ComboFilter ??= new ComboFilter();
                hook.ComboFilter.Mode = option.UsesComboIds
                    ? ComboFilterMode.ComboIds
                    : ComboFilterMode.AnyCombo;
                return;
            }

            hook.TriggerEvent = option.Event;
            hook.Subject = option.Subject;
        }

        /// <summary>
        /// El "cuándo" del hook en una frase, con los filtros que efectivamente lo achican.
        /// </summary>
        /// <remarks>
        /// Es lo que va en el panel y en el nodo del grafo. El nodo mostraba
        /// <c>"Kind: EventBus · Trigger Event: OnDiceRolled"</c>, que es el mismo dato en el idioma
        /// de quien escribió el motor.
        /// </remarks>
        public static string Describe(PassiveItemHook hook)
        {
            if (hook == null) return string.Empty;

            if (IsPermanent(hook)) return "Mientras lo tengas en el inventario";

            var option = Match(hook);
            var sb = new StringBuilder();

            if (option.HasValue) sb.Append(option.Value.DisplayName);
            else sb.Append("Disparador desconocido (").Append(hook.Kind == PassiveHookKind.ComboPlayed
                    ? "combo"
                    : hook.TriggerEvent.ToString()).Append(')');

            if (hook.Kind != PassiveHookKind.ComboPlayed) return sb.ToString();

            if (hook.ComboFilter != null
                && hook.ComboFilter.Mode == ComboFilterMode.ComboIds
                && hook.ComboFilter.ComboIds != null
                && hook.ComboFilter.ComboIds.Count > 0)
            {
                sb.Append(": ").Append(string.Join(", ", hook.ComboFilter.ComboIds));
            }

            if (hook.ActionKindFilter != RollActionKind.Unknown)
                sb.Append(" — sólo en ").Append(ActionKindLabel(hook.ActionKindFilter));

            return sb.ToString();
        }

        static string ActionKindLabel(RollActionKind kind)
        {
            switch (kind)
            {
                case RollActionKind.Attack: return "ataques";
                case RollActionKind.Defense: return "defensas";
                case RollActionKind.Heal: return "curaciones";
                default: return kind.ToString();
            }
        }
    }
}
