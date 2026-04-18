using System.Collections.Generic;
using Sirenix.OdinInspector;
using Sirenix.Serialization;
using UnityEngine;

namespace Rollgeon.Entities.Behaviors
{
    /// <summary>
    /// Libreria de templates de <see cref="BaseBehavior"/> indexados por string.
    /// TECHNICAL.md §7.2b. Permite compartir la MISMA definicion entre N enemigos
    /// sin duplicar por asset — cada spawn hace <see cref="GetClone"/> para obtener
    /// una instancia fresh con su propio <c>StoredValues</c> bag.
    /// </summary>
    /// <remarks>
    /// <para>
    /// <b>Por que no hereda de <c>BaseCatalogSO&lt;BaseBehavior&gt;</c>.</b> Los
    /// <c>BaseBehavior</c> no exponen un <c>Id</c> como campo — el id es la <b>key del
    /// diccionario externo</b>. Usar <c>BaseCatalogSO&lt;T&gt;</c> requeriria contaminar
    /// el contrato §7.2 con un <c>string TemplateId</c>. Plan §10.3.
    /// </para>
    /// <para>
    /// <b>Deep clone.</b> <see cref="GetClone"/> usa <see cref="SerializationUtility.CreateCopy"/>
    /// (Odin) para clonar el behavior polimorfico completo, incluyendo sus Effects y
    /// StoredValues bag vacio.
    /// </para>
    /// </remarks>
    [CreateAssetMenu(menuName = "Rollgeon/Behavior Library", fileName = "BehaviorLibrary")]
    public class BehaviorLibrarySO : SerializedScriptableObject
    {
        [Title("Templates")]
        [InfoBox("Cada entry mapea un id string a un BaseBehavior polimorfico. " +
                 "El GetClone(id) devuelve una copia fresh para el spawn de una entidad.")]
        [OdinSerialize]
        [DictionaryDrawerSettings(KeyLabel = "Template Id", ValueLabel = "Behavior")]
        private Dictionary<string, BaseBehavior> _templates = new Dictionary<string, BaseBehavior>();

        /// <summary>Ids declarados en la libreria. Alimenta <c>[ValueDropdown]</c> transversales.</summary>
        public IEnumerable<string> AllTemplateIds => _templates != null ? _templates.Keys : System.Array.Empty<string>();

        /// <summary><c>true</c> si existe un template bajo <paramref name="templateId"/>.</summary>
        public bool Contains(string templateId)
        {
            if (string.IsNullOrEmpty(templateId) || _templates == null) return false;
            return _templates.ContainsKey(templateId);
        }

        /// <summary>
        /// Devuelve una copia deep del template. <c>null</c> si no existe. La copia usa
        /// <see cref="SerializationUtility.CreateCopy"/> para preservar polimorfismo.
        /// </summary>
        public BaseBehavior GetClone(string templateId)
        {
            if (string.IsNullOrEmpty(templateId) || _templates == null) return null;
            if (!_templates.TryGetValue(templateId, out var template) || template == null) return null;
            return SerializationUtility.CreateCopy(template) as BaseBehavior;
        }

        /// <summary>
        /// API de edit-only / tests — agrega / upsertea un template bajo la key dada.
        /// Los tests populan via este metodo; el workflow de produccion lo edita en el
        /// inspector.
        /// </summary>
        public void SetTemplate(string templateId, BaseBehavior behavior)
        {
            if (string.IsNullOrEmpty(templateId)) return;
            if (_templates == null) _templates = new Dictionary<string, BaseBehavior>();
            _templates[templateId] = behavior;
        }
    }
}
