'use strict';

/*
 * ミニマップの画像を、ゲーム内と同じ色に塗って描く。
 *
 * ゲーム内の Unlit/MapShader と同じ計算を WebGL で行う。
 * 元の画像は赤系で描かれており、赤成分から青成分を引いた分だけが指定した色に置き換わる。
 * 赤みのない部分（影や枠）はそのまま残る。
 * Among Us は Gamma カラースペースなので、sRGB 値のまま計算する。
 */

const MapImage = (() => {
  const VERT = `
attribute vec2 aPos;
varying vec2 vUv;
void main(){
  vUv = aPos * 0.5 + 0.5;
  gl_Position = vec4(aPos, 0.0, 1.0);
}`;

  const FRAG = `
precision highp float;
varying vec2 vUv;
uniform sampler2D uTex;
uniform vec3 uColor;

void main(){
  vec4 c = texture2D(uTex, vUv);
  float d = c.r - c.b;
  vec3 tinted = uColor * d + c.b;
  //赤みの乏しいところは元の色のまま残す
  vec3 rgb = d > 0.1 ? tinted : c.rgb;
  gl_FragColor = vec4(clamp(rgb, 0.0, 1.0), c.a);
}`;

  let gl = null;
  let program = null;
  let failed = false;

  function hexToRgb(hex, fallback) {
    const m = /^#?([0-9a-f]{6})$/i.exec(hex || '');
    if (!m) return fallback;
    const n = parseInt(m[1], 16);
    return [((n >> 16) & 255) / 255, ((n >> 8) & 255) / 255, (n & 255) / 255];
  }

  function compile(type, src) {
    const s = gl.createShader(type);
    gl.shaderSource(s, src);
    gl.compileShader(s);
    if (!gl.getShaderParameter(s, gl.COMPILE_STATUS)) throw new Error(gl.getShaderInfoLog(s) || 'compile failed');
    return s;
  }

  /** 使い回す WebGL を用意する。使えない環境なら false。*/
  function setup() {
    if (failed) return false;
    if (gl) return true;

    try {
      const canvas = document.createElement('canvas');
      gl = canvas.getContext('webgl', { premultipliedAlpha: false, alpha: true, antialias: false, preserveDrawingBuffer: true });
      if (!gl) throw new Error('no webgl');

      program = gl.createProgram();
      gl.attachShader(program, compile(gl.VERTEX_SHADER, VERT));
      gl.attachShader(program, compile(gl.FRAGMENT_SHADER, FRAG));
      gl.bindAttribLocation(program, 0, 'aPos');
      gl.linkProgram(program);
      if (!gl.getProgramParameter(program, gl.LINK_STATUS)) throw new Error(gl.getProgramInfoLog(program) || 'link failed');

      const buffer = gl.createBuffer();
      gl.bindBuffer(gl.ARRAY_BUFFER, buffer);
      gl.bufferData(gl.ARRAY_BUFFER, new Float32Array([-1, -1, 3, -1, -1, 3]), gl.STATIC_DRAW);
      gl.enableVertexAttribArray(0);
      gl.vertexAttribPointer(0, 2, gl.FLOAT, false, 0, 0);

      return true;
    } catch (error) {
      //WebGL が無くても、塗る前の画像をそのまま出せば形は分かる。
      failed = true;
      gl = null;
      return false;
    }
  }

  /**
   * 画像を読み込んで塗り替え、data URI で返す。
   * 塗れなければ元の URL をそのまま返すので、呼び出し側はどちらでも同じに扱える。
   */
  async function render(src, color) {
    const source = await new Promise((resolve, reject) => {
      const img = new Image();
      img.onload = () => resolve(img);
      img.onerror = () => reject(new Error('cannot load ' + src));
      img.src = src;
    });

    if (!setup()) return src;

    const canvas = gl.canvas;
    canvas.width = source.naturalWidth;
    canvas.height = source.naturalHeight;

    const texture = gl.createTexture();
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
    gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
    gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.LINEAR);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
    gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

    gl.useProgram(program);
    gl.viewport(0, 0, canvas.width, canvas.height);
    gl.disable(gl.BLEND);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.activeTexture(gl.TEXTURE0);
    gl.uniform1i(gl.getUniformLocation(program, 'uTex'), 0);
    gl.uniform3fv(gl.getUniformLocation(program, 'uColor'), hexToRgb(color, [0.05, 0.2, 1]));

    gl.drawArrays(gl.TRIANGLES, 0, 3);

    const data = canvas.toDataURL('image/png');
    gl.deleteTexture(texture);
    return data;
  }

  return { render };
})();

//別のスクリプトから参照できるよう明示的に公開する。
window.MapImage = MapImage;
