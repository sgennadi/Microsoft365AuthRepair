/** Microsoft365AuthRepair standalone anonymous diagnostics receiver.
 * Deploy in a SEPARATE Google Apps Script project; never overwrite the live
 * shared Termsrv gateway until all clients have migrated.
 * Server-only Script Property: GITHUB_M365_ISSUES_TOKEN.
 */
const MAX_REPORT_LENGTH = 8192;
function output_(result) {
  return ContentService.createTextOutput(JSON.stringify(result))
    .setMimeType(ContentService.MimeType.JSON);
}
function sha256_(text) {
  const bytes = Utilities.computeDigest(
    Utilities.DigestAlgorithm.SHA_256, text, Utilities.Charset.UTF_8);
  return bytes.map(b => ('0' + (b & 255).toString(16)).slice(-2)).join('');
}
function doGet() {
  return output_({ok:true, service:'Microsoft365AuthRepair diagnostics',
    method:'POST', gateway_schema_version:1,
    capabilities:['microsoft365-auth-repair/v1','microsoft365-auth-repair/v2']});
}
const M365_REPORT_REPOSITORY = 'sgennadi/Microsoft365AuthRepair';
const M365_ISSUE_API = 'https://api.github.com/repos/' + M365_REPORT_REPOSITORY + '/issues';
const M365_TOKEN_PROPERTY = 'GITHUB_M365_ISSUES_TOKEN';
const M365_ISSUES_PER_UTC_DAY = 12;
const M365_FIELDS = Object.freeze([
  'schema', 'id', 'timestamp_utc', 'project', 'component',
  'error_type', 'code', 'level', 'version', 'platform',
  'architecture', 'fingerprint', 'signals'
]);
// Legacy six, already-published expanded 26, and new 29-field contracts.
// Do not relax the outer universal-diagnostics/v1 envelope or permit raw data.
const M365_SIGNAL_FIELDS_V1 = Object.freeze([
  'azure_ad_joined', 'domain_joined', 'device_auth_status',
  'azure_ad_prt', 'wam_default_set', 'broker_plugin'
]);
const M365_SIGNAL_FIELDS_V1_EXPANDED = Object.freeze([
  ...M365_SIGNAL_FIELDS_V1, 'office_installed', 'office_edition',
  'office_activation', 'office_wmi_records', 'office_licensed_records',
  'office_preview_records', 'office_preview_key_records', 'tpm_present',
  'tpm_version', 'tpm_ready', 'bitlocker_os_protection',
  'bitlocker_os_conversion', 'memory_integrity_runtime',
  'dpapi_protection_policy', 'dpapi_domain_joined', 'dpapi_writable_dc',
  'tpm_aik_enrollment', 'tpm_ek_cert_store', 'tpm_manufacturer_class',
  'windows_tpm_release'
]);
const M365_SIGNAL_FIELDS_V2 = Object.freeze([
  ...M365_SIGNAL_FIELDS_V1_EXPANDED,
  'secure_boot_state', 'memory_integrity_configured', 'device_probe_coverage'
]);
const M365_SIGNAL_ALLOWED = Object.freeze({
  azure_ad_joined: ['YES','NO','UNKNOWN'],
  domain_joined: ['YES','NO','UNKNOWN'],
  device_auth_status: ['SUCCESS','FAILED','UNKNOWN'],
  azure_ad_prt: ['YES','NO','UNKNOWN'],
  wam_default_set: ['YES','NO','UNKNOWN'],
  broker_plugin: ['PRESENT','MISSING','UNKNOWN'],
  office_installed: ['YES','NO','UNKNOWN'],
  office_edition: ['PROPLUS_2024','STANDARD_2024','VISIO_2024','PROJECT_2024',
    'MULTIPLE_VOLUME','VOLUME_OTHER','MICROSOFT_365','RETAIL','UNKNOWN'],
  office_activation: ['LICENSED','UNLICENSED','PREVIEW_KEY_PRESENT','NOT_VERIFIED','UNKNOWN'],
  office_wmi_records: ['NONE','ONE','TWO','THREE_PLUS','NOT_APPLICABLE','UNKNOWN'],
  office_licensed_records: ['NONE','ONE','TWO','THREE_PLUS','NOT_APPLICABLE','UNKNOWN'],
  office_preview_records: ['NONE','ONE','TWO','THREE_PLUS','NOT_APPLICABLE','UNKNOWN'],
  office_preview_key_records: ['NONE','ONE','TWO','THREE_PLUS','NOT_APPLICABLE','UNKNOWN'],
  tpm_present: ['YES','NO','UNKNOWN'],
  tpm_version: ['1.2','2.0','UNKNOWN'],
  tpm_ready: ['YES','NO','UNKNOWN'],
  bitlocker_os_protection: ['ON','OFF','UNKNOWN'],
  bitlocker_os_conversion: ['FULLY_DECRYPTED','FULLY_ENCRYPTED','ENCRYPTING',
    'DECRYPTING','ENCRYPTION_PAUSED','DECRYPTION_PAUSED','UNKNOWN'],
  memory_integrity_runtime: ['RUNNING','NOT_RUNNING','UNKNOWN'],
  dpapi_protection_policy: ['NOT_SET','ZERO','ONE','OTHER','UNKNOWN'],
  dpapi_domain_joined: ['YES','NO','UNKNOWN'],
  dpapi_writable_dc: ['AVAILABLE','NOT_FOUND','UNKNOWN','NOT_APPLICABLE'],
  tpm_aik_enrollment: ['NOT_FOUND','TIMEOUT','INVALID_PUBLIC_KEY','SERVER_ERROR',
    'OTHER_ERROR','NONE_RECORDED','UNKNOWN'],
  tpm_ek_cert_store: ['IMPORT_INCOMPLETE','UNKNOWN'],
  tpm_manufacturer_class: ['STM','MSFT','OTHER','UNKNOWN'],
  windows_tpm_release: ['WIN11_24H2','WIN11_25H2','WIN11_NEWER','OTHER','UNKNOWN'],
  secure_boot_state: ['ON','OFF','UNSUPPORTED','UNKNOWN'],
  memory_integrity_configured: ['ON','OFF','NOT_SET','UNKNOWN'],
  device_probe_coverage: ['COMPLETE','PARTIAL','NOT_VERIFIED']
});
function validM365Signals_(signals) {
  if (!signals || typeof signals !== 'object' || Array.isArray(signals)) return false;
  const keys = Object.keys(signals);
  const contracts = [M365_SIGNAL_FIELDS_V1, M365_SIGNAL_FIELDS_V1_EXPANDED,
    M365_SIGNAL_FIELDS_V2];
  const matched = contracts.some(fields => keys.length === fields.length &&
    keys.every(key => fields.includes(key)));
  return matched && keys.every(key => typeof signals[key] === 'string' &&
    M365_SIGNAL_ALLOWED[key].includes(signals[key]));
}

function validM365Report_(input) {
  if (!input || typeof input !== 'object' || Array.isArray(input) ||
      Object.keys(input).length !== 1 ||
      !Object.prototype.hasOwnProperty.call(input, 'event')) return false;
  const r = input.event;
  if (!r || typeof r !== 'object' || Array.isArray(r) ||
      Object.keys(r).length !== M365_FIELDS.length ||
      Object.keys(r).some(k => !M365_FIELDS.includes(k))) return false;
  if (r.schema !== 'universal-diagnostics/v1' ||
      r.project !== M365_REPORT_REPOSITORY ||
      r.platform !== 'Windows' ||
      !['AMD64', 'ARM64'].includes(r.architecture) ||
      typeof r.id !== 'string' ||
      !/^[0-9a-f]{8}-[0-9a-f]{4}-4[0-9a-f]{3}-[89ab][0-9a-f]{3}-[0-9a-f]{12}$/.test(r.id) ||
      typeof r.component !== 'string' ||
      !/^[A-Za-z][A-Za-z0-9 _-]{0,55}$/.test(r.component) ||
      typeof r.error_type !== 'string' ||
      !/^[A-Za-z][A-Za-z0-9_]{0,63}$/.test(r.error_type) ||
      typeof r.code !== 'string' ||
      !/^(?:0x[A-Fa-f0-9]{1,8}|[A-Za-z0-9_-]{1,32})$/.test(r.code) ||
      !['info', 'warning', 'error'].includes(r.level) ||
      typeof r.version !== 'string' ||
      !/^[0-9]+\.[0-9]+\.[0-9]+(?:-[A-Za-z0-9.-]+)?$/.test(r.version) ||
      typeof r.timestamp_utc !== 'string' ||
      !/^[0-9]{4}-[0-9]{2}-[0-9]{2}T[0-9]{2}:[0-9]{2}:[0-9]{2}Z$/.test(r.timestamp_utc) ||
      typeof r.fingerprint !== 'string' ||
      !/^[a-f0-9]{64}$/.test(r.fingerprint)) return false;

  if (!validM365Signals_(r.signals)) return false;

  const seen = Date.parse(r.timestamp_utc), now = Date.now();
  if (!Number.isFinite(seen) ||
      seen > now + 5 * 60 * 1000 ||
      seen < now - 90 * 24 * 3600 * 1000 ||
      new Date(seen).toISOString().replace('.000Z', 'Z') !== r.timestamp_utc)
    return false;
  const expected = sha256_(
    [r.project, r.component, r.error_type, r.code]
      .map(v => v.toLowerCase()).join('|'));
  return r.fingerprint === expected ? r : false;
}

// Only hardcoded labels and previously validated enum values are published.
function m365IssueBody_(r) {
  const groups = [
    ['Identity / Entra / WAM', [
      ['Azure AD joined','azure_ad_joined'], ['Domain joined','domain_joined'],
      ['Device authentication','device_auth_status'], ['Azure AD PRT','azure_ad_prt'],
      ['WAM default set','wam_default_set'], ['AAD Broker Plugin','broker_plugin']
    ]],
    ['Office licensing', [
      ['Office installed','office_installed'], ['Office edition','office_edition'],
      ['Office activation','office_activation'],
      ['Office WMI count band','office_wmi_records'],
      ['Licensed product count band','office_licensed_records'],
      ['Preview product count band','office_preview_records'],
      ['Preview key count band','office_preview_key_records']
    ]],
    ['Device security', [
      ['TPM present','tpm_present'], ['TPM version','tpm_version'],
      ['TPM ready','tpm_ready'], ['OS BitLocker protection','bitlocker_os_protection'],
      ['OS BitLocker conversion','bitlocker_os_conversion'],
      ['Memory Integrity running','memory_integrity_runtime'],
      ['Memory Integrity configured','memory_integrity_configured'],
      ['Secure Boot','secure_boot_state'],
      ['DPAPI policy','dpapi_protection_policy'],
      ['DPAPI domain joined','dpapi_domain_joined'],
      ['Writable DC available','dpapi_writable_dc'],
      ['AIK enrollment','tpm_aik_enrollment'],
      ['EK certificate import','tpm_ek_cert_store'],
      ['TPM manufacturer category','tpm_manufacturer_class'],
      ['Windows release category','windows_tpm_release'],
      ['Read-only provider coverage','device_probe_coverage']
    ]]
  ];
  const sections = groups.map(([title, fields]) => {
    const lines = fields.filter(([, key]) =>
      Object.prototype.hasOwnProperty.call(r.signals, key))
      .map(([label, key]) => '- ' + label + ': ' + r.signals[key]);
    return lines.length ? '### ' + title + '\n' + lines.join('\n') + '\n\n' : '';
  }).join('');
  return '<!-- udx-report-id:' + r.id + ' -->\n' +
    '## Microsoft 365 Auth Repair: Anonymous UniversalDiagnostics\n\n' +
    '- Schema: ' + r.schema + '\n' + '- Project: ' + r.project + '\n' +
    '- Component: ' + r.component + '\n' + '- Version: ' + r.version + '\n' +
    '- Severity: ' + r.level + '\n' + '- Error class: ' + r.error_type + '\n' +
    '- Error code: ' + r.code + '\n' + '- Observed UTC: ' + r.timestamp_utc + '\n' +
    '- OS: ' + r.platform + '\n' + '- Architecture: ' + r.architecture + '\n' +
    '- Fingerprint: ' + r.fingerprint + '\n\n' + sections +
    'Allowlisted categories only. No raw summaries, logs, keys, recovery secrets, ' +
    'identities or device identifiers.\n';
}

function publishM365_(report, token) {
  const payload = JSON.stringify({
    title: '[Diagnostics] Microsoft365AuthRepair ' + report.id.slice(0, 8),
    body: m365IssueBody_(report)
  });
  const response = UrlFetchApp.fetch(M365_ISSUE_API, {
    method: 'post',
    contentType: 'application/json',
    payload: payload,
    followRedirects: false,
    muteHttpExceptions: true,
    headers: {
      Authorization: 'Bearer ' + token,
      Accept: 'application/vnd.github+json',
      'X-GitHub-Api-Version': '2022-11-28',
      'User-Agent': 'UniversalDiagnostics-ReportGateway'
    }
  });
  if (response.getResponseCode() !== 201)
    return {ok: false, error: 'github_not_confirmed'};
  let result;
  try { result = JSON.parse(response.getContentText()); }
  catch (_) { return {ok: false, error: 'github_receipt_invalid'}; }
  const url = String(result.html_url || '');
  if (!/^https:\/\/github\.com\/sgennadi\/Microsoft365AuthRepair\/issues\/[1-9][0-9]*$/.test(url))
    return {ok: false, error: 'github_receipt_invalid'};
  return {ok: true, issue_url: url};
}


function doPost(event) {
  try {
    if (!event || !event.postData ||
        typeof event.postData.contents !== 'string' ||
        event.postData.contents.length > MAX_REPORT_LENGTH)
      return output_({ok:false,error:'invalid_request'});
    let input;
    try { input = JSON.parse(event.postData.contents); }
    catch (_) { return output_({ok:false,error:'invalid_json'}); }
    const report = validM365Report_(input);
    if (!report) return output_({ok:false,error:'invalid_evidence'});
    const properties = PropertiesService.getScriptProperties();
    const token = properties.getProperty(M365_TOKEN_PROPERTY);
    if (!token) return output_({ok:false,error:'not_configured'});
    const lock = LockService.getScriptLock();
    if (!lock.tryLock(2500)) return output_({ok:false,error:'busy'});
    try {
      const cache = CacheService.getScriptCache();
      const dedupKey = 'm365_' + report.id;
      const cached = cache.get(dedupKey);
      if (cached) return output_({ok:true,issue_url:cached,duplicate:true});
      const day = Utilities.formatDate(new Date(),'Etc/UTC','yyyyMMdd');
      const countKey = 'm365_issues_' + day;
      const count = Number(properties.getProperty(countKey)||'0');
      if (count >= M365_ISSUES_PER_UTC_DAY)
        return output_({ok:false,error:'daily_limit'});
      const receipt = publishM365_(report,token);
      if (!receipt.ok) return output_(receipt);
      properties.setProperty(countKey,String(count+1));
      cache.put(dedupKey,receipt.issue_url,21600);
      return output_(receipt);
    } finally { lock.releaseLock(); }
  } catch (_) { return output_({ok:false,error:'gateway_error'}); }
}
