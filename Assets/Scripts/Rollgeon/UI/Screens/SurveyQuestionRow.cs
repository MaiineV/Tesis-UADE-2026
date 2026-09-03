using System.Collections.Generic;
using Rollgeon.Survey;
using Sirenix.OdinInspector;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace Rollgeon.UI.Screens
{
    /// <summary>
    /// Una fila del cuestionario de evento (Feature#0074). Un solo componente para
    /// los tres prefabs de fila: Rating y Choice clonan <see cref="_optionTemplate"/>
    /// por opción (5 números o N textos), Text usa el <see cref="_textInput"/>.
    /// Las refs que no aplican al prefab quedan vacías.
    /// </summary>
    [AddComponentMenu("Rollgeon/UI/Survey/Survey Question Row")]
    public sealed class SurveyQuestionRow : MonoBehaviour
    {
        private const string LogPrefix = "[SurveyQuestionRow] ";
        private const int RatingSteps = 5;

        [Title("Row — común")]
        [Required("Label del enunciado.")]
        [SerializeField] private TMP_Text _questionLabel;

        [Tooltip("Marca de obligatoria (*). Opcional.")]
        [SerializeField] private GameObject _requiredMark;

        [Tooltip("Marco/fondo que se prende cuando falta responder. Opcional.")]
        [SerializeField] private Graphic _invalidFrame;

        [Title("Rating / Choice")]
        [Tooltip("Toggle desactivado que se clona por opción. Debe tener un TMP_Text hijo para el texto.")]
        [SerializeField] private Toggle _optionTemplate;

        [Tooltip("Contenedor con Layout donde se instancian las opciones. Vacío = padre del template.")]
        [SerializeField] private Transform _optionContainer;

        [Tooltip("ToggleGroup compartido por las opciones (allowSwitchOff = true).")]
        [SerializeField] private ToggleGroup _optionGroup;

        [Title("Text")]
        [SerializeField] private TMP_InputField _textInput;

        private readonly List<Toggle> _options = new List<Toggle>();
        private SurveyQuestion _question;

        public string QuestionId => _question?.Id;
        public SurveyQuestionType Type => _question?.Type ?? SurveyQuestionType.Rating1to5;
        public bool Required => _question != null && _question.Required;

        /// <summary>Configura la fila para la pregunta. Borra cualquier respuesta previa.</summary>
        public void Bind(SurveyQuestion question, string localeCode)
        {
            _question = question;
            if (question == null) return;

            Relabel(localeCode);
            if (_requiredMark != null) _requiredMark.SetActive(question.Required);
            SetInvalid(false);

            switch (question.Type)
            {
                case SurveyQuestionType.Rating1to5:
                    BuildOptions(RatingLabels());
                    break;

                case SurveyQuestionType.SingleChoice:
                    BuildOptions(question.GetOptions(localeCode));
                    break;

                case SurveyQuestionType.FreeText:
                    if (_textInput != null)
                    {
                        _textInput.characterLimit = Mathf.Max(1, question.MaxLength);
                        _textInput.text = string.Empty;
                    }
                    else
                    {
                        Debug.LogWarning(LogPrefix + $"'{question.Id}' es FreeText pero el prefab no tiene TMP_InputField.", this);
                    }
                    break;
            }
        }

        /// <summary>Cambio de idioma en vivo: solo textos, sin tocar lo ya respondido.</summary>
        public void Relabel(string localeCode)
        {
            if (_question == null) return;

            if (_questionLabel != null) _questionLabel.text = _question.GetText(localeCode);

            if (_question.Type == SurveyQuestionType.SingleChoice)
            {
                var options = _question.GetOptions(localeCode);
                for (int i = 0; i < _options.Count && i < options.Count; i++)
                {
                    SetOptionLabel(_options[i], options[i]);
                }
            }
        }

        /// <summary><c>false</c> si no hay respuesta (texto vacío o ninguna opción).</summary>
        public bool TryGetValue(out string value)
        {
            value = null;
            if (_question == null) return false;

            switch (_question.Type)
            {
                case SurveyQuestionType.Rating1to5:
                {
                    int index = SelectedOptionIndex();
                    if (index < 0) return false;
                    value = (index + 1).ToString();
                    return true;
                }

                case SurveyQuestionType.SingleChoice:
                {
                    int index = SelectedOptionIndex();
                    if (index < 0) return false;
                    value = index.ToString();
                    return true;
                }

                case SurveyQuestionType.FreeText:
                {
                    var text = _textInput != null ? _textInput.text?.Trim() : null;
                    if (string.IsNullOrEmpty(text)) return false;
                    value = text;
                    return true;
                }
            }

            return false;
        }

        public void SetInvalid(bool invalid)
        {
            if (_invalidFrame != null) _invalidFrame.enabled = invalid;
        }

        // ====================================================================
        // Opciones
        // ====================================================================

        private void BuildOptions(IReadOnlyList<string> labels)
        {
            ClearOptions();

            if (_optionTemplate == null)
            {
                Debug.LogWarning(LogPrefix + $"'{_question.Id}' necesita opciones pero el prefab no tiene template.", this);
                return;
            }

            var container = _optionContainer != null ? _optionContainer : _optionTemplate.transform.parent;
            _optionTemplate.gameObject.SetActive(false);

            for (int i = 0; i < labels.Count; i++)
            {
                var toggle = Instantiate(_optionTemplate, container);
                toggle.name = $"Option_{i}";
                toggle.group = _optionGroup;
                toggle.isOn = false;
                toggle.gameObject.SetActive(true);
                SetOptionLabel(toggle, labels[i]);
                _options.Add(toggle);
            }
        }

        private void ClearOptions()
        {
            foreach (var toggle in _options)
            {
                if (toggle == null) continue;
                // EditMode (tests) no admite Destroy diferido.
                if (Application.isPlaying) Destroy(toggle.gameObject);
                else DestroyImmediate(toggle.gameObject);
            }
            _options.Clear();
        }

        private int SelectedOptionIndex()
        {
            for (int i = 0; i < _options.Count; i++)
            {
                if (_options[i] != null && _options[i].isOn) return i;
            }
            return -1;
        }

        private static void SetOptionLabel(Toggle toggle, string text)
        {
            if (toggle == null) return;
            var label = toggle.GetComponentInChildren<TMP_Text>(true);
            if (label != null) label.text = text;
        }

        private static IReadOnlyList<string> RatingLabels()
        {
            var labels = new string[RatingSteps];
            for (int i = 0; i < RatingSteps; i++) labels[i] = (i + 1).ToString();
            return labels;
        }

        /// <summary>Selecciona una opción por índice sin pasar por el EventSystem (tests, navegación por teclado).</summary>
        public void SelectOption(int index)
        {
            if (index < 0 || index >= _options.Count) return;
            _options[index].isOn = true;
        }

        public int OptionCount => _options.Count;

        public void SetText(string text)
        {
            if (_textInput != null) _textInput.text = text;
        }

        private void OnDestroy() => ClearOptions();
    }
}
