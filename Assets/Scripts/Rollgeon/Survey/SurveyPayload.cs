using System;
using UnityEngine;

namespace Rollgeon.Survey
{
    /// <summary>
    /// Serialización de <see cref="SurveyResponse"/>. Dos formatos: el que va a
    /// disco (sin secreto: la PC del stand es compartida) y el que viaja al Apps
    /// Script (con <c>secret</c>). <c>JsonUtility</c> escapa comillas, barras,
    /// saltos de línea y unicode por su cuenta.
    /// </summary>
    public static class SurveyPayload
    {
        /// <summary>Wire format: la respuesta más el secreto compartido, al mismo nivel.</summary>
        [Serializable]
        private sealed class WireEnvelope : SurveyResponse
        {
            public string secret;
        }

        public static string ToStoredJson(SurveyResponse response)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));
            return JsonUtility.ToJson(response);
        }

        public static SurveyResponse FromStoredJson(string json)
        {
            if (string.IsNullOrWhiteSpace(json)) return null;
            try
            {
                return JsonUtility.FromJson<SurveyResponse>(json);
            }
            catch (Exception)
            {
                return null;
            }
        }

        public static string ToWireJson(SurveyResponse response, string secret)
        {
            if (response == null) throw new ArgumentNullException(nameof(response));

            var wire = new WireEnvelope
            {
                response_id = response.response_id,
                event_id = response.event_id,
                created_at = response.created_at,
                app_version = response.app_version,
                run_id = response.run_id,
                floor_index = response.floor_index,
                hero_id = response.hero_id,
                locale = response.locale,
                device_id = response.device_id,
                raffle_opt_in = response.raffle_opt_in,
                email = response.email ?? string.Empty,
                answers = response.answers,
                secret = secret ?? string.Empty,
            };
            return JsonUtility.ToJson(wire);
        }
    }
}
