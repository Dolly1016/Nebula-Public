'use strict';

/*
 * ゲーム記録の閲覧画面。
 * サーバーが返す時刻は ISO 8601 の世界標準時。表示の直前にローカル時刻へ変換する。
 */

const $ = (id) => document.getElementById(id);

// ---------------------------------------------------------------- 文言

/**
 * 文言は言語別の CSS（text.*.css）がカスタムプロパティとして持っている。
 * 英語の上に使用中の言語を重ねて読み込んであるので、ここでは値を読むだけでよい。
 * 使用中の言語に無いキーは、カスケードの結果として英語が残る。
 */
const Text = (() => {
  const style = getComputedStyle(document.documentElement);
  const cache = new Map();

  //CSS の文字列リテラルを素の文字列に戻す。エスケープもほどく。
  const unquote = (raw) => {
    const value = raw.trim();
    if (value.length < 2) return '';

    const quote = value[0];
    if ((quote !== '"' && quote !== "'") || value[value.length - 1] !== quote) return value;

    return value.slice(1, -1).replace(/\\([0-9a-fA-F]{1,6})[ \t\n]?|\\(.)/g,
      (_, hex, ch) => (hex ? String.fromCodePoint(parseInt(hex, 16)) : ch));
  };

  return {
    get(key) {
      if (!cache.has(key)) cache.set(key, unquote(style.getPropertyValue('--t-' + key)));
      return cache.get(key);
    },
  };
})();

/** 文言を引く。{0} があれば value で埋める。未定義ならキーをそのまま返して気付けるようにする。*/
function t(key, value) {
  const text = Text.get(key) || key;
  return value === undefined ? text : text.replace('{0}', value);
}

/** {0} の位置に要素を挟んだ断片を返す。数字だけ強調したいときに使う。*/
function fillNode(key, node) {
  const [before, after = ''] = t(key).split('{0}');
  return [document.createTextNode(before), node, document.createTextNode(after)];
}

/** 画面に最初から書いてある文言を、読み込んだ言語で置き換える。*/
function applyStaticText() {
  document.documentElement.lang = t('html-lang');
  document.title = t('page-title');

  for (const el of document.querySelectorAll('[data-t]')) el.textContent = t(el.dataset.t);
  for (const el of document.querySelectorAll('[data-t-title]')) el.title = t(el.dataset.tTitle);
  for (const el of document.querySelectorAll('[data-t-aria]')) el.setAttribute('aria-label', t(el.dataset.tAria));

  const count = document.createElement('strong');
  count.textContent = String(UNMARKED_LIMIT);
  $('list-limit').replaceChildren(...fillNode('list-limit', count));
}

const statusEl = $('status');
const gameEl = $('game');
const listEl = $('game-list');
const overlayEl = $('overlay');

/** マークしていない記録が残る件数。サーバー側の GameRecordStore.Trim と揃えておく。*/
const UNMARKED_LIMIT = 100;

/** 一覧（新しい順）。*/
let summaries = [];
/** 'all' | 'marked' */
let filter = 'all';
/** 表示中のゲームの id。*/
let currentId = null;
/** 表示中のゲームの全内容。*/
let current = null;

// ---------------------------------------------------------------- 表示の道具

/** 時刻の表示は分単位まで。*/
const TIME_FORMAT = {
  year: 'numeric', month: '2-digit', day: '2-digit',
  hour: '2-digit', minute: '2-digit',
};

/** ISO 8601 の UTC 文字列をローカル時刻の表示にする。*/
function formatLocal(iso) {
  if (!iso) return '—';
  const date = new Date(iso);
  if (Number.isNaN(date.getTime())) return '—';
  return date.toLocaleString(undefined, TIME_FORMAT);
}

/** 時刻を <time> で返す。原文の UTC は datetime 属性に残す。*/
function timeNode(iso) {
  if (!iso) return document.createTextNode('—');
  const node = document.createElement('time');
  node.dateTime = iso;
  node.textContent = formatLocal(iso);
  return node;
}

function setText(id, text) {
  $(id).textContent = text || '—';
}

function replaceChild(id, node) {
  const host = $(id);
  host.replaceChildren(node);
}

/** 記録に載っている最終的な勝利条件。*/
function finalStageOf(record) {
  const stages = record?.end?.stages;
  return stages && stages.length > 0 ? stages[stages.length - 1] : null;
}

function playerNameOf(record, playerId) {
  const player = record?.players?.find((p) => p.playerId === playerId);
  return player ? player.name : t('player-unknown');
}

function showError(message) {
  gameEl.hidden = true;
  statusEl.hidden = false;
  statusEl.classList.add('is-error');
  statusEl.textContent = message;
}

// ---------------------------------------------------------------- 役職アイコン

/**
 * 役職アイコンは、ゲーム内で作られたアトラス1枚から必要なマスだけを切り出して出す。
 * どのシートのどのマスかは /api/role-icons の対応表が持っている。
 */
const RoleIcons = (() => {
  let manifest = null;

  async function load() {
    try {
      const response = await fetch('/api/role-icons');
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      manifest = await response.json();
    } catch (error) {
      //アイコンが出ないだけで画面は成り立つ。黙って諦める。
      console.warn('役職アイコンの対応表を読み込めませんでした。', error);
      return;
    }

    //取得前に描かれていた分にも後から効かせる
    apply(document);
  }

  /** 差し込まれた .role-icon に、アトラスのどこを切り取るかを教える。*/
  function apply(root) {
    for (const el of root.querySelectorAll('.role-icon')) {
      //対応表が後から届いたときの二度がけを避ける
      if (el.classList.contains('is-ready')) continue;

      const entry = manifest?.icons?.[el.dataset.icon];
      if (!entry) continue;

      const [sheet, cell] = entry;
      const name = manifest.sheets[sheet];
      if (name === undefined) continue;

      el.style.backgroundImage = `url(/role-icons/${encodeURIComponent(name)}.png)`;
      el.style.setProperty('--cols', String(manifest.columns));
      el.style.setProperty('--rows', String(manifest.rows));
      el.style.setProperty('--col', String(cell % manifest.columns));
      el.style.setProperty('--row', String(Math.floor(cell / manifest.columns)));
      el.classList.add('is-ready');

      //何のアイコンかはカーソルを合わせたときに見せる
      attachTip(el, manifest.names?.[el.dataset.icon]);
    }
  }

  return { load, apply };
})();

// ---------------------------------------------------------------- 移動経路と出来事

/**
 * ゲームを「ターン」と「会議」に区切り、それぞれの様子を見せる。
 *
 * ターンでは足取りをミニマップ上で再生でき、会議では直前のターンの最終位置を映したまま
 * その会議で起きたことを並べる。マップの色でどちらを見ているかが分かるようにしてある。
 */
const RouteViewer = (() => {
  /** GameStatistics.EventVariation の Id。 */
  const VARIATION = { GAME_START: 2, GAME_END: 3, MEETING_END: 4, REPORT: 5, EMERGENCY: 6 };

  /** 区切りの境目になる出来事。ゲーム開始・会議開始・会議終了・ゲーム終了。 */
  const TURN_STARTERS = new Set([VARIATION.GAME_START, VARIATION.MEETING_END]);
  const MEETING_STARTERS = new Set([VARIATION.REPORT, VARIATION.EMERGENCY]);

  /** 会議中のマップの色。青でも赤でもない、沈んだ色にする。 */
  const MEETING_COLOR = '#79808f';

  const section = $('route');
  const emptyEl = $('route-empty');
  const canvasEl = $('route-canvas');
  const markersEl = $('route-markers');
  const segmentsEl = $('route-segments');
  const controlsEl = $('route-controls');
  const eventsEl = $('route-events');
  const playButton = $('route-play');
  const seekEl = $('route-seek');
  const timeEl = $('route-time');
  const showAliveEl = $('route-show-alive');
  const showGhostsEl = $('route-show-ghosts');

  /** マップの寸法と座標の変換に要る値。マップIDで引く。*/
  let maps = null;

  /** 塗り替え済みのマップ画像。通常と会議中の2枚を控える。*/
  let images = { normal: null, meeting: null };

  let mapInfo = null;
  let segments = [];
  let segment = null;

  /**
   * ゲーム開始の時刻。
   * 記録してある時刻は NebulaGameManager.CurrentTime で、ゲーム開始より前から進んでいる。
   * 区切りごとの原点が取れないときの控えとして使う。
   */
  let gameStart = 0;

  /** 選んでいる出来事。会議中は位置を映さないので、印には効かない。*/
  let selectedEvent = null;

  /** マップ上の印。プレイヤーIDで引く。*/
  let markers = new Map();

  /** 再生位置。点の番号。整数でない値も取るので補間して描ける。*/
  let cursor = 0;
  let playing = false;
  let frame = null;
  let lastTick = 0;

  // ---------------------------------------------------------------- 読み込み

  async function loadMaps() {
    if (maps) return maps;

    try {
      const response = await fetch('/api/maps');
      if (!response.ok) throw new Error(`HTTP ${response.status}`);
      maps = new Map((await response.json()).map((m) => [m.mapId, m]));
    } catch (error) {
      console.warn('ミニマップの情報を読み込めませんでした。', error);
      maps = new Map();
    }
    return maps;
  }

  /** 圧縮された足取りを展開する。展開できなければ空。*/
  async function inflate(packed) {
    if (!packed) return [];
    if (typeof DecompressionStream === 'undefined') {
      console.warn('このブラウザは DecompressionStream を持っていません。');
      return [];
    }

    try {
      const bytes = Uint8Array.from(atob(packed), (c) => c.charCodeAt(0));
      const stream = new Blob([bytes]).stream().pipeThrough(new DecompressionStream('gzip'));
      return (await new Response(stream).json()) ?? [];
    } catch (error) {
      console.warn('足取りを展開できませんでした。', error);
      return [];
    }
  }

  async function show(record) {
    stop();
    section.hidden = true;
    emptyEl.hidden = true;
    segments = [];
    segment = null;

    const [all, phases] = await Promise.all([loadMaps(), inflate(record.movement)]);

    //表示中のゲームが切り替わっていたら、遅れて届いた結果は捨てる。
    if (record !== current) return;

    mapInfo = all.get(record.mapId) ?? null;

    const usable = phases.filter(hasSamples);
    //出来事が無い記録では、最初のターンの始まりを代用する。
    gameStart = record.timeOrigin ?? usable[0]?.startTime ?? 0;
    segments = buildSegments(record.events ?? [], usable);

    if (segments.length === 0 || !mapInfo) {
      emptyEl.hidden = false;
      return;
    }

    section.hidden = false;
    await drawMap();
    buildSegmentButtons();
    select(0);
  }

  const hasSamples = (phase) => (phase.tracks ?? []).some((t) => (t.states?.length ?? 0) > 0);

  /**
   * ゲーム開始・会議開始・会議終了・ゲーム終了の時刻で、ターンと会議に区切る。
   *
   * 区切りの境目は出来事の時刻そのもので決める。足取りは後から、
   * その時間帯に重なっている部分だけを切り出して結びつける。
   * こうすると、目盛りと出来事の一覧が同じ原点を指す。
   */
  function buildSegments(events, phases) {
    const list = [];
    let turns = 0;
    let meetings = 0;

    const close = (time) => { if (list.length > 0) list[list.length - 1].end = time; };

    const open = (kind, time) => {
      close(time);
      const seg = { kind, number: kind === 'turn' ? ++turns : ++meetings, start: time, end: Infinity, events: [] };
      list.push(seg);
      return seg;
    };

    let currentSegment = null;
    for (const event of events) {
      if (TURN_STARTERS.has(event.variation)) currentSegment = open('turn', event.time);
      else if (MEETING_STARTERS.has(event.variation)) currentSegment = open('meeting', event.time);
      else if (!currentSegment) currentSegment = open('turn', event.time);

      currentSegment.events.push(event);

      //ゲーム終了でその区切りは打ち止め。
      if (event.variation === VARIATION.GAME_END) close(event.time);
    }

    //出来事を記録していない古い記録でも、足取りだけは見られるようにする。
    if (list.length === 0) {
      for (const phase of phases) open('turn', phase.startTime).end = endOf(phase);
    }

    for (const seg of list) attachPhase(seg, phases, list);
    return list;
  }

  const countOf = (phase) => phase.tracks?.[0]?.states?.length ?? 0;
  const endOf = (phase) => phase.startTime + Math.max(0, countOf(phase) - 1) * phase.interval;

  /**
   * 区切りに足取りを結びつける。
   *
   * ターンには、その時間帯に最も重なる足取りの、重なっている範囲だけを切り出して持たせる。
   * 会議には直前のターンの切り出しをそのまま渡し、その最後の点を最終位置として使う。
   */
  function attachPhase(seg, phases, list) {
    if (seg.kind === 'meeting') {
      const previous = list.slice(0, list.indexOf(seg)).reverse().find((s) => s.kind === 'turn');
      seg.phase = previous?.phase ?? null;
      seg.offset = previous?.offset ?? 0;
      seg.count = previous?.count ?? 0;
      return;
    }

    let best = null;
    let bestOverlap = 0;
    for (const phase of phases) {
      const overlap = Math.min(seg.end, endOf(phase)) - Math.max(seg.start, phase.startTime);
      if (overlap > bestOverlap) { best = phase; bestOverlap = overlap; }
    }

    if (!best) {
      seg.phase = null;
      seg.offset = 0;
      seg.count = 0;
      return;
    }

    const offset = Math.max(0, Math.round((seg.start - best.startTime) / best.interval));
    const available = countOf(best) - offset;
    const wanted = Number.isFinite(seg.end) ? Math.round((seg.end - seg.start) / best.interval) + 1 : available;

    seg.phase = best;
    seg.offset = offset;
    seg.count = Math.max(0, Math.min(wanted, available));
  }

  // ---------------------------------------------------------------- マップ

  async function drawMap() {
    canvasEl.style.aspectRatio = `${mapInfo.width} / ${mapInfo.height}`;

    const src = `/maps/${mapInfo.mapId}.png`;
    //会議用の色違いも先に作っておく。切り替えるたびに描き直さずに済む。
    images.normal = await MapImage.render(src, mapInfo.color);
    images.meeting = await MapImage.render(src, MEETING_COLOR);
  }

  function buildMarkers() {
    markersEl.replaceChildren();
    markers = new Map();

    for (const player of current?.players ?? []) {
      const el = iconOf(player);
      el.className = 'route-marker';
      attachTip(el, player.name);
      markersEl.appendChild(el);
      markers.set(player.playerId, el);
    }

    //ニセモノは本物と同じ見た目で、区別が付くよう薄く縁取る。
    for (const fake of segment?.phase?.fakeTracks ?? []) {
      const el = iconOf({ color: fake.color?.main, shadowColor: fake.color?.shadow, visorColor: fake.color?.visor });
      el.className = 'route-marker is-fake';
      attachTip(el, reasonOf(fake), ownerOf(fake));
      markersEl.appendChild(el);
      markers.set(fakeKey(fake.fakeId), el);
    }
  }

  /** ニセモノの印は本物のプレイヤーIDと衝突しない鍵で持つ。*/
  const fakeKey = (fakeId) => 'fake:' + fakeId;

  /** 湧いた理由。分からなければ「ニセモノ」とだけ。*/
  const reasonOf = (fake) => current?.fakeReasons?.[fake.reasonTranslationKey] || t('route-fake');

  /** 呼び出した人。分からなければ null。*/
  const ownerOf = (fake) =>
    fake.ownerId != null ? t('route-fake-owner', playerNameOf(current, fake.ownerId)) : null;

  /** ゲーム内の座標をミニマップ上の割合にする。*/
  function toPercent(x, y) {
    const px = (x / mapInfo.scale + mapInfo.centerX) * mapInfo.pixelsPerUnit + mapInfo.width / 2;
    const py = mapInfo.height / 2 - (y / mapInfo.scale + mapInfo.centerY) * mapInfo.pixelsPerUnit;
    return [(px / mapInfo.width) * 100, (py / mapInfo.height) * 100];
  }

  /** サイドパネルの選択に当てはまるか。*/
  function isShown(state) {
    const dead = (state & 1) !== 0;
    return dead ? showGhostsEl.checked : showAliveEl.checked;
  }

  function place(playerId, x, y, state) {
    const el = markers.get(playerId);
    if (!el) return;

    if (!isShown(state)) {
      el.hidden = true;
      return;
    }

    const [left, top] = toPercent(x, y);
    el.hidden = false;
    el.style.left = `${left}%`;
    el.style.top = `${top}%`;
    el.classList.toggle('is-dead', (state & 1) !== 0);
    el.classList.toggle('is-invisible', (state & 2) !== 0);
  }

  function hideAllMarkers() {
    for (const el of markers.values()) el.hidden = true;
  }

  // ---------------------------------------------------------------- 区切りの選択

  function buildSegmentButtons() {
    segmentsEl.replaceChildren();

    segments.forEach((seg, index) => {
      const button = document.createElement('button');
      button.type = 'button';
      button.className = 'chip' + (seg.kind === 'meeting' ? ' is-meeting' : '');
      button.textContent = t(seg.kind === 'meeting' ? 'route-meeting' : 'route-turn', seg.number);
      button.addEventListener('click', () => select(index));
      segmentsEl.appendChild(button);
    });
  }

  function select(index) {
    stop();
    segment = segments[index];
    selectedEvent = null;

    for (const [i, button] of [...segmentsEl.children].entries()) button.classList.toggle('is-active', i === index);

    //会議は止まった絵なので、再生の操作は出さない。
    const isMeeting = segment.kind === 'meeting';
    canvasEl.style.backgroundImage = `url(${isMeeting ? images.meeting : images.normal})`;
    canvasEl.classList.toggle('is-meeting', isMeeting);
    controlsEl.hidden = isMeeting || sampleCount() === 0;

    seekEl.max = String(Math.max(0, sampleCount() - 1));
    cursor = 0;

    buildMarkers();
    buildEventList();
    render();
  }

  const sampleCount = () => (segment?.kind === 'turn' ? segment.count ?? 0 : 0);
  const interval = () => segment?.phase?.interval ?? 0.25;

  // ---------------------------------------------------------------- 描画

  function render() {
    if (!segment) return;

    //会議中は最終位置を映したままにする。出来事を選んでも動かさない。
    if (segment.kind === 'meeting') {
      renderLastSample();
      return;
    }

    if (selectedEvent?.positions?.length > 0) {
      renderEventPositions(selectedEvent);
      return;
    }

    renderTrack();
  }

  /** 足取りの、いまの再生位置。*/
  function renderTrack() {
    const count = sampleCount();
    if (count === 0) {
      hideAllMarkers();
      return;
    }

    //シークバーが指すのは区切りの中での位置。足取りを引くときだけ offset を足す。
    const local = Math.min(Math.floor(cursor), count - 1);
    const index = segment.offset + local;
    const next = Math.min(index + 1, segment.offset + count - 1);
    const ratio = cursor - Math.floor(cursor);

    hideAllMarkers();
    for (const track of segment.phase.tracks) {
      const points = track.points;

      //点と点の間はまっすぐ繋いで補間する。0.25 秒ぶんなので直線で足りる。
      const x = points[index * 2] + (points[next * 2] - points[index * 2]) * ratio;
      const y = points[index * 2 + 1] + (points[next * 2 + 1] - points[index * 2 + 1]) * ratio;
      place(track.playerId, x, y, track.states[index] ?? 0);
    }

    renderFakes(local, ratio);

    seekEl.value = String(local);
    updateTime();
  }

  /**
   * その時点にいたニセモノ。まだ湧いていない・もう消えたものは出さない。
   * 点と点の間は本物と同じように直線で補間する。
   */
  function renderFakes(local, ratio) {
    for (const fake of segment.phase?.fakeTracks ?? []) {
      const at = local - fake.startIndex;
      if (at < 0 || at >= fake.states.length) continue;

      const next = Math.min(at + 1, fake.states.length - 1);
      const points = fake.points;
      const x = points[at * 2] + (points[next * 2] - points[at * 2]) * ratio;
      const y = points[at * 2 + 1] + (points[next * 2 + 1] - points[at * 2 + 1]) * ratio;

      place(fakeKey(fake.fakeId), x, y, fake.states[at] ?? 0);
    }
  }

  /** 直前のターンの最後の様子。会議中はこれを映し続ける。*/
  function renderLastSample() {
    hideAllMarkers();
    if (!segment.phase || (segment.count ?? 0) === 0) return;

    const last = segment.offset + segment.count - 1;
    for (const track of segment.phase.tracks) {
      if (last * 2 + 1 >= track.points.length) continue;
      place(track.playerId, track.points[last * 2], track.points[last * 2 + 1], track.states[last] ?? 0);
    }

    renderFakes(last, 0);
  }

  /**
   * 出来事が控えている位置。
   * 生死は出来事側に無いので、同じ時刻の足取りから借りる。絞り込みの判断に要る。
   */
  function renderEventPositions(event) {
    hideAllMarkers();

    const index = segment.offset + Math.min(Math.floor(cursor), Math.max(0, sampleCount() - 1));
    const stateAt = (playerId) =>
      segment.phase?.tracks?.find((t) => t.playerId === playerId)?.states?.[index] ?? 0;

    for (const p of event.positions) place(p.playerId, p.x, p.y, stateAt(p.playerId));
    updateTime();
  }

  function formatElapsed(seconds) {
    const total = Math.max(0, Math.floor(seconds));
    return `${Math.floor(total / 60)}:${String(total % 60).padStart(2, '0')}`;
  }

  /** その区切りが始まってからの経過時間。境目の出来事の時刻が原点。*/
  const elapsedIn = (time) => time - (segment?.start ?? gameStart);

  /** 「今どこか / この区切りはどこまでか」を出す。*/
  function updateTime() {
    const last = Math.max(0, sampleCount() - 1);
    timeEl.textContent = `${formatElapsed(cursor * interval())} / ${formatElapsed(last * interval())}`;
  }

  // ---------------------------------------------------------------- 出来事の一覧

  function buildEventList() {
    eventsEl.replaceChildren();

    if (segment.events.length === 0) {
      const empty = document.createElement('li');
      empty.className = 'route-event is-empty';
      empty.textContent = t('route-no-events');
      eventsEl.appendChild(empty);
      return;
    }

    for (const event of segment.events) {
      const item = document.createElement('li');
      item.className = 'route-event';

      const time = document.createElement('span');
      time.className = 'time';
      time.textContent = formatElapsed(elapsedIn(event.time));

      const what = document.createElement('span');
      what.className = 'what';
      what.textContent = event.detailText || event.detail;

      const who = document.createElement('span');
      who.className = 'who';
      who.textContent = describePlayers(event);

      item.append(time, what, who);

      //会議中は位置を映さないので、選んでも印は動かない。選んでいる印だけ付ける。
      item.addEventListener('click', () => selectEvent(event, item));
      eventsEl.appendChild(item);
    }
  }

  function describePlayers(event) {
    const name = (id) => playerNameOf(current, id);
    const targets = event.targetIds.map(name).join(', ');

    if (event.sourceId == null) return targets;
    if (targets.length === 0) return name(event.sourceId);
    return `${name(event.sourceId)} → ${targets}`;
  }

  function selectEvent(event, item) {
    stop();
    selectedEvent = event;

    for (const el of eventsEl.children) el.classList.toggle('is-selected', el === item);

    //シークバーをその出来事の時刻へ動かす。位置を控えていない出来事では、
    //そこの足取りがそのまま印の置き場所になる。
    if (segment.kind === 'turn' && segment.phase) {
      const at = (event.time - segment.start) / interval();
      cursor = Math.min(Math.max(at, 0), Math.max(0, sampleCount() - 1));
      seekEl.value = String(Math.floor(cursor));
    }

    render();
  }

  // ---------------------------------------------------------------- 再生

  function play() {
    if (playing || sampleCount() === 0) return;

    //終わりまで来ていたら頭から流し直す
    if (cursor >= sampleCount() - 1) cursor = 0;

    selectedEvent = null;
    for (const el of eventsEl.children) el.classList.remove('is-selected');

    playing = true;
    playButton.classList.add('is-active');
    playButton.title = t('route-pause');
    lastTick = performance.now();
    frame = requestAnimationFrame(tick);
  }

  function stop() {
    playing = false;
    playButton.classList.remove('is-active');
    playButton.title = t('route-play');
    if (frame !== null) cancelAnimationFrame(frame);
    frame = null;
  }

  function tick(now) {
    if (!playing) return;

    //実時間で進める。記録の間隔で割れば点の番号になる。
    cursor += ((now - lastTick) / 1000) / interval();
    lastTick = now;

    if (cursor >= sampleCount() - 1) {
      cursor = sampleCount() - 1;
      render();
      stop();
      return;
    }

    render();
    frame = requestAnimationFrame(tick);
  }

  for (const toggle of [showAliveEl, showGhostsEl]) toggle.addEventListener('change', () => render());

  playButton.addEventListener('click', () => (playing ? stop() : play()));
  seekEl.addEventListener('input', () => {
    stop();
    selectedEvent = null;
    for (const el of eventsEl.children) el.classList.remove('is-selected');
    cursor = Number(seekEl.value);
    render();
  });

  return { show, stop };
})();

// ---------------------------------------------------------------- サイドバー

function renderList() {
  const shown = summaries.filter((s) => (filter === 'marked' ? s.marked : true));

  listEl.replaceChildren();

  if (shown.length === 0) {
    const empty = document.createElement('p');
    empty.className = 'sidebar-foot';
    empty.textContent = t(summaries.length === 0 ? 'list-empty' : 'list-empty-marked');
    listEl.appendChild(empty);
    return;
  }

  for (const summary of shown) {
    const button = document.createElement('button');
    button.type = 'button';
    button.className = 'game-link' + (summary.id === currentId ? ' is-current' : '');

    const date = document.createElement('span');
    date.className = 'date';
    date.textContent = formatLocal(summary.startedAt || summary.endedAt);
    if (summary.marked) {
      const star = document.createElement('span');
      star.className = 'star';
      star.textContent = '★';
      date.appendChild(star);
    }

    const cond = document.createElement('span');
    cond.className = 'cond';
    cond.textContent = summary.conditionText || t('result-none');

    button.append(date, cond);
    button.addEventListener('click', () => selectGame(summary.id));
    listEl.appendChild(button);
  }
}

// ---------------------------------------------------------------- ゲーム本体

function renderGame(record) {
  const stage = finalStageOf(record);

  $('game-title').textContent = stage?.conditionText || t('result-none');

  replaceChild('fact-started', timeNode(record.startedAt));
  replaceChild('fact-ended', timeNode(record.endedAt));

  setText('fact-map', record.mapName);
  setText('fact-host', (record.players || []).find((p) => p.isHost)?.name);
  setText('fact-win', stage?.conditionText);

  const summary = summaries.find((s) => s.id === record.id);
  const marked = summary ? summary.marked : false;
  const markButton = $('mark');
  markButton.setAttribute('aria-pressed', String(marked));
  markButton.title = t(marked ? 'action-unmark' : 'action-mark');

  $('detail-open').disabled = !record.end;

  statusEl.hidden = true;
  gameEl.hidden = false;

  //展開に時間がかかるので待たせない。間に合った時点で下に現れる。
  RouteViewer.show(record);
}

// ---------------------------------------------------------------- 詳細

function renderDetail(record) {
  const winners = new Set(finalStageOf(record)?.winners ?? []);

  //人数が多いほどポップアップを広げる。実際の幅の決め方は style.css の .overlay-panel を参照。
  overlayEl.querySelector('.overlay-panel')
    .style.setProperty('--players', String((record.players || []).length));

  // --- プレイヤーごとの勝敗と結末
  const rows = document.createDocumentFragment();

  for (const player of record.players || []) {
    const tr = document.createElement('tr');
    const won = winners.has(player.playerId);
    if (won) tr.classList.add('is-winner');

    // 勝者にだけ印を付ける。負けた側には何も出さない。
    const winTd = document.createElement('td');
    if (won) {
      const badge = document.createElement('span');
      badge.className = 'badge win';
      badge.textContent = t('badge-win');
      winTd.appendChild(badge);
    }

    const nameTd = document.createElement('td');
    const nameBox = document.createElement('div');
    nameBox.className = 'player-name';
    const name = document.createElement('span');
    name.textContent = player.name;
    nameBox.appendChild(name);
    if (player.isHost) {
      const host = document.createElement('span');
      host.className = 'host';
      host.textContent = t('badge-host');
      nameBox.appendChild(host);
    }
    nameTd.appendChild(nameBox);
    if (player.isDisconnected) {
      const dc = document.createElement('span');
      dc.className = 'dc';
      dc.textContent = t('badge-disconnected');
      nameTd.appendChild(dc);
    }

    const roleTd = document.createElement('td');
    if (player.roleHtml) {
      //ゲーム内の表示そのまま。サーバー側でタグ以外はエスケープ済み。
      const box = document.createElement('div');
      box.className = 'role-name';
      box.innerHTML = player.roleHtml;
      RoleIcons.apply(box);
      roleTd.appendChild(box);
    } else {
      //表示名を持たない古い記録。訳し直せる役職名で代用する。
      roleTd.textContent = player.role || '—';
    }

    //役職の下に、アビリティなどが名乗る追加情報を添える。
    if (player.moreHtml) {
      const more = document.createElement('div');
      more.className = 'role-more';
      more.innerHTML = player.moreHtml;
      RoleIcons.apply(more);
      roleTd.appendChild(more);
    }
    //モディファイアは役職名に印として入っているので、別途テキストでは出さない。

    const taskTd = document.createElement('td');
    taskTd.className = 'col-task';
    //ゲーム終了時の表示そのまま。色もそちらに揃う。
    if (player.taskHtml) taskTd.innerHTML = player.taskHtml;

    const stateTd = document.createElement('td');
    stateTd.textContent = player.state || t(player.isDead ? 'state-dead' : 'state-alive');
    // 死因の補足。状態そのものに含まれない「誰にキルされたか」はここに出す。
    const extra = player.stateExtra
      || (player.killerId != null ? t('state-killer', playerNameOf(record, player.killerId)) : null);
    if (extra) {
      const extraEl = document.createElement('span');
      extraEl.className = 'state-extra';
      extraEl.textContent = extra;
      stateTd.appendChild(extraEl);
    }

    tr.append(winTd, nameTd, roleTd, taskTd, stateTd);
    rows.appendChild(tr);
  }

  $('player-rows').replaceChildren(rows);

  // --- 勝敗計算の経緯（プレイヤーを横に並べた縦のタイムライン）
  const stages = record.end?.stages ?? [];
  const players = record.players ?? [];
  const list = document.createDocumentFragment();

  stages.forEach((stage, index) => {
    const block = document.createElement('section');
    block.className = 'stage';

    const head = document.createElement('div');
    head.className = 'stage-head';

    const no = document.createElement('span');
    no.className = 'no';
    //最初の段階はそのゲームで実際に起きた勝利、以降はそれを乗っ取った勝利。
    no.textContent = index === 0 ? t('stage-first') : t('stage-usurped', index);

    const cond = document.createElement('span');
    cond.className = 'cond';
    cond.textContent = stage.conditionText || stage.condition;

    head.append(no, cond);
    //最初の段階は見出しで分かるので印は付けない。最終結果だけ目印を出す。
    if (index === stages.length - 1) head.appendChild(tag(t('stage-final')));

    const meta = document.createElement('div');
    meta.className = 'stage-meta';
    meta.textContent = t('stage-reason', stage.reasonText || stage.reason);

    block.append(head, meta);

    const phases = stage.phases ?? [];
    if (phases.length > 0 && players.length > 0) block.appendChild(buildTimeline(phases, players));

    list.appendChild(block);
  });

  $('stage-list').replaceChildren(list);
}

/**
 * フェーズを縦に、プレイヤーを横に並べたタイムラインを組む。
 */
function buildTimeline(phases, players) {
  const timeline = document.createElement('div');
  timeline.className = 'timeline';

  const grid = document.createElement('div');
  grid.className = 'tl-grid';
  grid.style.setProperty('--players', String(players.length));

  // --- 見出し行。アイコンだけを並べる。
  const head = document.createElement('div');
  head.className = 'tl-row tl-head';
  head.appendChild(document.createElement('div')).className = 'tl-label';

  for (const player of players) {
    const cell = document.createElement('div');
    cell.className = 'tl-player';
    cell.style.setProperty('--pc', fillColorOf(player));
    cell.appendChild(iconOf(player));
    //名前は常時は出さず、カーソルを合わせたときだけ吹き出しで見せる。
    attachTip(cell, player.name);

    head.appendChild(cell);
  }
  grid.appendChild(head);

  // --- フェーズごとの行
  phases.forEach((phase, phaseIndex) => {
    const row = document.createElement('div');
    row.className = 'tl-row tl-phase';

    const label = document.createElement('div');
    label.className = 'tl-label';

    const name = document.createElement('div');
    name.className = 'tl-phase-name';
    name.textContent = phase.nameText || phase.name;
    label.appendChild(name);

    const reasons = phase.reasons ?? [];
    if (reasons.length > 0) {
      const ul = document.createElement('ul');
      ul.className = 'tl-reasons';
      reasons.forEach((reason, reasonIndex) => {
        const li = document.createElement('li');
        li.className = 'tl-reason';
        li.dataset.phase = String(phaseIndex);
        li.dataset.reason = String(reasonIndex);
        li.textContent = reason.reasonText || reason.reason;
        attachTip(li, reason.players.map((id) => playerNameOf(current, id)).join(', '));
        ul.appendChild(li);
      });
      label.appendChild(ul);
    }

    row.appendChild(label);

    const winners = new Set(phase.winners ?? []);
    //一つ前のフェーズ終了時点の勝者。最初のフェーズより前では誰も勝っていない。
    const prevWinners = new Set(phaseIndex > 0 ? (phases[phaseIndex - 1].winners ?? []) : []);

    for (const player of players) {
      const cell = document.createElement('div');
      cell.className = 'tl-cell';
      cell.dataset.phase = String(phaseIndex);
      cell.dataset.player = String(player.playerId);

      const was = prevWinners.has(player.playerId);
      const now = winners.has(player.playerId);
      if (now) cell.classList.add('is-winner');
      if (was || now) cell.style.setProperty('--pc', fillColorOf(player));

      //このフェーズでこのプレイヤーに理由が付いているか
      if (reasons.some((r) => r.players.includes(player.playerId))) cell.classList.add('has-reason');

      const bar = document.createElement('div');
      bar.className = 'tl-bar';
      if (was || now) {
        //勝ち始めならこのフェーズの真ん中から丸く始め、勝ちを失うならこのフェーズの真ん中で丸く終える。
        //勝ったまま続くあいだはフェーズの箱の隙間を跨いで真っ直ぐ繋ぎ、最後まで勝っていれば下まで突き抜ける。
        bar.classList.add(was ? 'from-above' : 'from-middle');
        bar.classList.add(now ? 'to-bottom' : 'to-middle');
        if (!was) bar.classList.add('cap-top');
        if (!now) bar.classList.add('cap-bottom');
      }

      const dot = document.createElement('div');
      dot.className = 'dot';
      cell.append(bar, dot);
      row.appendChild(cell);
    }

    grid.appendChild(row);
  });

  timeline.appendChild(grid);
  attachHighlight(timeline, phases);
  return timeline;
}

/**
 * プレイヤーの色でセルを塗る。暗すぎると背景に沈むので、その場合だけ白を混ぜる。
 */
function fillColorOf(player) {
  const rgb = parseHex(player.color);
  if (!rgb) return '#8a93a8';

  // 明度（最大チャンネル）で判定する。輝度だと赤のような彩度の高い色まで暗い扱いになってしまう。
  const value = Math.max(rgb[0], rgb[1], rgb[2]) / 255;
  const mix = value < 0.55 ? Math.min(0.45, 0.55 - value) : 0;
  const lighten = (v) => Math.round(v + (255 - v) * mix);
  return `rgb(${lighten(rgb[0])}, ${lighten(rgb[1])}, ${lighten(rgb[2])})`;
}

function parseHex(hex) {
  const m = /^#?([0-9a-f]{6})$/i.exec(hex || '');
  if (!m) return null;
  const n = parseInt(m[1], 16);
  return [(n >> 16) & 255, (n >> 8) & 255, n & 255];
}

/** プレイヤーのアイコン。塗り替えた画像が用意できていればそれを、無ければ色付きの図形を返す。*/
function iconOf(player) {
  const data = PlayerIcon.render({ main: player.color, shadow: player.shadowColor, visor: player.visorColor });
  if (data) {
    const img = document.createElement('img');
    img.src = data;
    img.alt = player.name;
    return img;
  }
  const fallback = document.createElement('div');
  fallback.className = 'fallback';
  return fallback;
}

/**
 * プレイヤーと理由の対応を、カーソルを合わせたときに見せる。
 */
function attachHighlight(timeline, phases) {
  const clear = () => {
    for (const el of timeline.querySelectorAll('.is-lit, .is-dim')) el.classList.remove('is-lit', 'is-dim');
  };

  const dimAll = (selector) => {
    for (const el of timeline.querySelectorAll(selector)) el.classList.add('is-dim');
  };

  //セルにカーソル: そのフェーズで、そのプレイヤーに付いた理由を光らせる
  for (const cell of timeline.querySelectorAll('.tl-cell')) {
    cell.addEventListener('mouseenter', () => {
      clear();
      const phaseIndex = Number(cell.dataset.phase);
      const playerId = Number(cell.dataset.player);
      const reasons = phases[phaseIndex]?.reasons ?? [];

      dimAll('.tl-reason');
      cell.classList.add('is-lit');

      reasons.forEach((reason, reasonIndex) => {
        if (!reason.players.includes(playerId)) return;
        const el = timeline.querySelector(`.tl-reason[data-phase="${phaseIndex}"][data-reason="${reasonIndex}"]`);
        if (el) { el.classList.remove('is-dim'); el.classList.add('is-lit'); }
      });
    });
    cell.addEventListener('mouseleave', clear);
  }

  //理由にカーソル: その理由が当てはまるプレイヤーのセルを明るくする
  for (const el of timeline.querySelectorAll('.tl-reason')) {
    el.addEventListener('mouseenter', () => {
      clear();
      const phaseIndex = Number(el.dataset.phase);
      const reason = phases[phaseIndex]?.reasons?.[Number(el.dataset.reason)];
      if (!reason) return;

      el.classList.add('is-lit');
      for (const cell of timeline.querySelectorAll(`.tl-cell[data-phase="${phaseIndex}"]`)) {
        if (reason.players.includes(Number(cell.dataset.player))) cell.classList.add('is-lit');
        else cell.classList.add('is-dim');
      }
    });
    el.addEventListener('mouseleave', clear);
  }
}

/* --- 自前の吹き出し。ブラウザ標準の title は使わない。 */

let tipElement = null;

/**
 * カーソルを合わせたときの吹き出しを取り付ける。
 * sub を渡すと、2行目に一回り小さい文字で添える。
 */
function attachTip(target, text, sub) {
  if (!text) return;
  target.addEventListener('mouseenter', () => showTip(target, text, sub));
  target.addEventListener('mouseleave', hideTip);
}

function showTip(anchor, text, sub) {
  if (!tipElement) {
    tipElement = document.createElement('div');
    tipElement.className = 'tl-tip';
    tipElement.hidden = true;
    document.body.appendChild(tipElement);
  }

  tipElement.replaceChildren(document.createTextNode(text));
  if (sub) {
    const line = document.createElement('div');
    line.className = 'tl-tip-sub';
    line.textContent = sub;
    tipElement.appendChild(line);
  }
  tipElement.hidden = false;

  //position:fixed なので画面上の座標をそのまま使える。対象の真上に置く。
  const rect = anchor.getBoundingClientRect();
  tipElement.style.left = `${rect.left + rect.width / 2}px`;
  tipElement.style.top = `${rect.top - 6}px`;
}

function hideTip() {
  if (tipElement) tipElement.hidden = true;
}

function tag(text) {
  const el = document.createElement('span');
  el.className = 'tag';
  el.textContent = text;
  return el;
}

function openOverlay() {
  if (!current?.end) return;

  try {
    renderDetail(current);
  } catch (error) {
    //描画に失敗しても黙って閉じたままにしない。何が起きたのか見えるようにする。
    console.error(error);
    $('player-rows').replaceChildren();

    const message = document.createElement('p');
    message.className = 'status is-error';
    message.textContent = t('error-detail') + '\n'
      + (error && error.stack ? error.stack : String(error));
    message.style.whiteSpace = 'pre-wrap';
    $('stage-list').replaceChildren(message);
  }

  overlayEl.hidden = false;
  document.body.style.overflow = 'hidden';
}

function closeOverlay() {
  hideTip();
  overlayEl.hidden = true;
  document.body.style.overflow = '';
}

// ---------------------------------------------------------------- 取得

async function selectGame(id) {
  currentId = id;
  renderList();

  try {
    const response = await fetch(`/api/games/${encodeURIComponent(id)}`);
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    current = await response.json();
    current.id = current.id || id;
    renderGame(current);
  } catch (error) {
    current = null;
    showError(t('error-record', error.message));
  }
}

async function load() {
  statusEl.hidden = false;
  statusEl.classList.remove('is-error');
  statusEl.textContent = t('status-loading');

  try {
    const response = await fetch('/api/games');
    if (!response.ok) throw new Error(`HTTP ${response.status}`);
    summaries = await response.json();
  } catch (error) {
    showError(t('error-load', error.message));
    return;
  }

  renderList();

  if (summaries.length === 0) {
    gameEl.hidden = true;
    statusEl.hidden = false;
    statusEl.classList.remove('is-error');
    statusEl.textContent = t('status-empty');
    return;
  }

  // トップページは最新のゲーム。表示中のものがあれば保つ。
  const keep = summaries.some((s) => s.id === currentId) ? currentId : summaries[0].id;
  await selectGame(keep);
}

async function toggleMark() {
  if (!current) return;

  const summary = summaries.find((s) => s.id === current.id);
  const next = !(summary ? summary.marked : false);
  const button = $('mark');
  button.disabled = true;

  try {
    const response = await fetch(`/api/games/${encodeURIComponent(current.id)}/mark`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify({ marked: next }),
    });
    if (!response.ok) throw new Error(`HTTP ${response.status}`);

    if (summary) summary.marked = next;
    button.setAttribute('aria-pressed', String(next));
    button.title = t(next ? 'action-unmark' : 'action-mark');
    renderList();
  } catch (error) {
    showError(t('error-mark', error.message));
  } finally {
    button.disabled = false;
  }
}

// ---------------------------------------------------------------- 起動

for (const button of document.querySelectorAll('[data-filter]')) {
  button.addEventListener('click', () => {
    filter = button.dataset.filter;
    for (const other of document.querySelectorAll('[data-filter]')) {
      other.classList.toggle('is-active', other === button);
    }
    renderList();
  });
}

$('reload').addEventListener('click', load);
$('mark').addEventListener('click', toggleMark);
$('detail-open').addEventListener('click', openOverlay);

for (const closer of overlayEl.querySelectorAll('[data-close]')) {
  closer.addEventListener('click', closeOverlay);
}
document.addEventListener('keydown', (event) => {
  if (event.key === 'Escape' && !overlayEl.hidden) closeOverlay();
});

//アイコンの塗り替えに使う画像とWebGLを裏で用意しておく。
//画面の読み込みを待たせない。間に合わなければ色付きの代替表示になるだけ。
applyStaticText();
PlayerIcon.init('/here_icon.png');
RoleIcons.load();
load();
