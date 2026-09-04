/**
 * Rollgeon — backend del cuestionario de evento (Feature#0074).
 *
 * Google Apps Script VINCULADO a la planilla (Extensiones → Apps Script), desplegado
 * como Web App. El juego hace POST con el JSON de SurveyResponse (+ secret) como
 * text/plain; cada respuesta cae como fila en la pestaña que nombra `event_id`
 * (una pestaña por evento; se crea sola con su header).
 *
 * Setup y troubleshooting: docs/setup/event-survey.md
 */

// Igual que SurveyConfig.SharedSecret en Unity. Vacío = no se chequea.
var SHARED_SECRET = '';

// Columnas fijas, en este orden. Las preguntas se agregan como q_<id> al final.
var BASE_COLUMNS = [
  'received_at', 'created_at', 'response_id', 'app_version', 'run_id',
  'floor_index', 'hero_id', 'locale', 'device_id', 'raffle_opt_in', 'email'
];

function doPost(e) {
  var lock = LockService.getScriptLock();
  lock.waitLock(10000);
  try {
    var body = JSON.parse(e.postData.contents);

    if (SHARED_SECRET && body.secret !== SHARED_SECRET) {
      return json_({ ok: false, error: 'unauthorized' });
    }
    if (!body.response_id) {
      return json_({ ok: false, error: 'missing response_id' });
    }

    var sheet = getOrCreateSheet_(SpreadsheetApp.getActiveSpreadsheet(), sanitizeTab_(body.event_id));
    var answers = body.answers || [];
    ensureColumns_(sheet, answers.map(function (a) { return 'q_' + a.id; }));

    // El cliente reintenta tras un timeout aunque acá ya se haya escrito.
    if (isDuplicate_(sheet, body.response_id)) {
      return json_({ ok: true, duplicate: true });
    }

    sheet.appendRow(buildRow_(sheet, body, answers));
    return json_({ ok: true });
  } catch (err) {
    return json_({ ok: false, error: String(err) });
  } finally {
    lock.releaseLock();
  }
}

// Sanity check desde el navegador: abrir la URL /exec y ver {"ok":true,"ping":true}.
function doGet() {
  return json_({ ok: true, ping: true });
}

function json_(obj) {
  return ContentService
    .createTextOutput(JSON.stringify(obj))
    .setMimeType(ContentService.MimeType.JSON);
}

function sanitizeTab_(eventId) {
  var name = String(eventId || 'sin-evento').replace(/[\[\]:*?\/\\]/g, '-').trim();
  return name.substring(0, 100) || 'sin-evento';
}

function getOrCreateSheet_(spreadsheet, name) {
  var sheet = spreadsheet.getSheetByName(name);
  if (sheet) return sheet;
  sheet = spreadsheet.insertSheet(name);
  sheet.appendRow(BASE_COLUMNS);
  sheet.setFrozenRows(1);
  return sheet;
}

function headers_(sheet) {
  var lastCol = sheet.getLastColumn();
  if (lastCol === 0) return [];
  return sheet.getRange(1, 1, 1, lastCol).getValues()[0].map(String);
}

// Una pregunta nueva no rompe nada: su columna se agrega al final.
function ensureColumns_(sheet, questionColumns) {
  var headers = headers_(sheet);
  if (headers.length === 0) {
    sheet.appendRow(BASE_COLUMNS);
    headers = BASE_COLUMNS.slice();
  }
  var missing = questionColumns.filter(function (c) { return headers.indexOf(c) < 0; });
  if (missing.length === 0) return;
  sheet.getRange(1, headers.length + 1, 1, missing.length).setValues([missing]);
}

function isDuplicate_(sheet, responseId) {
  var headers = headers_(sheet);
  var col = headers.indexOf('response_id') + 1;
  if (col <= 0 || sheet.getLastRow() < 2) return false;
  var found = sheet.getRange(2, col, sheet.getLastRow() - 1, 1)
    .createTextFinder(String(responseId))
    .matchEntireCell(true)
    .findNext();
  return found !== null;
}

// Mapea por nombre de columna: el orden en la planilla es libre.
function buildRow_(sheet, body, answers) {
  var headers = headers_(sheet);
  var values = {
    received_at: new Date().toISOString(),
    created_at: body.created_at || '',
    response_id: body.response_id || '',
    app_version: body.app_version || '',
    run_id: body.run_id || '',
    floor_index: typeof body.floor_index === 'number' ? body.floor_index : '',
    hero_id: body.hero_id || '',
    locale: body.locale || '',
    device_id: body.device_id || '',
    raffle_opt_in: body.raffle_opt_in === true,
    email: body.email || ''
  };
  answers.forEach(function (a) { values['q_' + a.id] = a.value == null ? '' : String(a.value); });

  return headers.map(function (h) { return values.hasOwnProperty(h) ? values[h] : ''; });
}
