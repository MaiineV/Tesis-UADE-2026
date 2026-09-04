using System.Collections.Generic;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Preguntas default con las que el menú de setup crea <c>SurveyConfig.asset</c>.
    /// Vive en runtime (no en Editor) para que los tests validen que el default es
    /// una config válida.
    /// </summary>
    public static class SurveyConfigDefaults
    {
        public const string DefaultEventId = "evento-2026";

        /// <summary>Pisa <paramref name="config"/> con los valores de fábrica.</summary>
        public static void Populate(SurveyConfigSO config)
        {
            if (config == null) return;

            config.Enabled = false;
            config.EventId = DefaultEventId;
            config.TriggerFloorIndex = 0;
            config.EndpointUrl = string.Empty;
            config.SharedSecret = string.Empty;
            config.TimeoutSeconds = 10;
            config.AskEmailForRaffle = true;
            config.AutoCloseSeconds = 1.5f;
            config.Questions = BuildDefaultQuestions();
        }

        public static List<SurveyQuestion> BuildDefaultQuestions()
        {
            return new List<SurveyQuestion>
            {
                new SurveyQuestion
                {
                    Id = "fun",
                    Type = SurveyQuestionType.Rating1to5,
                    TextEs = "¿Cuánto te divertiste?",
                    TextEn = "How much fun did you have?",
                    Required = true,
                },
                new SurveyQuestion
                {
                    Id = "clarity",
                    Type = SurveyQuestionType.Rating1to5,
                    TextEs = "¿Qué tan claras fueron las reglas de los dados?",
                    TextEn = "How clear were the dice rules?",
                    Required = true,
                },
                new SurveyQuestion
                {
                    Id = "favorite",
                    Type = SurveyQuestionType.SingleChoice,
                    TextEs = "¿Qué fue lo que más te gustó?",
                    TextEn = "What did you like the most?",
                    OptionsEs = new List<string> { "Los dados", "Los combos", "Los enemigos", "El arte", "Otro" },
                    OptionsEn = new List<string> { "The dice", "The combos", "The enemies", "The art", "Other" },
                    Required = true,
                },
                new SurveyQuestion
                {
                    Id = "change",
                    Type = SurveyQuestionType.FreeText,
                    TextEs = "¿Qué cambiarías?",
                    TextEn = "What would you change?",
                    Required = false,
                    MaxLength = 240,
                },
            };
        }
    }
}
