'use strict';

/*
 * MapBehaviour.HereIcon のスプライトを、プレイヤーの配色に塗り替えて描く。
 *
 * ゲーム内の Unlit/PlayerShader と同じ計算を WebGL で行う。
 * 元のスプライトは純赤・純緑・純青で描かれており、それぞれ体・バイザー・影の色に置き換わる。
 * グレースケール（輪郭の黒など）はそのまま残る。
 * Among Us は Gamma カラースペースなので、sRGB 値のまま計算する。
 */

const PlayerIcon = (() => {
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
uniform vec3 uBody;
uniform vec3 uVisor;
uniform vec3 uShadow;

// r≒g≒b（グレー軸から約8度以内）なら 1。その色は置き換えない。
float grayMask(vec3 c){
  vec3 n = c * inversesqrt(max(dot(c, c), 1e-12));
  return clamp(floor(dot(n, vec3(0.5773502692)) + 0.01), 0.0, 1.0);
}

void main(){
  vec4 c = texture2D(uTex, vUv);
  vec3 mapped = c.r * uBody + c.g * uVisor + c.b * uShadow;
  gl_FragColor = vec4(mix(mapped, c.rgb, grayMask(c.rgb)), c.a);
}`;

  let gl = null;
  let program = null;
  let texture = null;
  let source = null;
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

  /** 画像を読み込んで WebGL を用意する。使えない環境なら false。*/
  async function init(src) {
    if (failed) return false;
    if (gl) return true;

    try {
      source = await new Promise((resolve, reject) => {
        const img = new Image();
        img.onload = () => resolve(img);
        img.onerror = () => reject(new Error('cannot load ' + src));
        img.src = src;
      });

      const canvas = document.createElement('canvas');
      canvas.width = source.naturalWidth;
      canvas.height = source.naturalHeight;

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

      texture = gl.createTexture();
      gl.bindTexture(gl.TEXTURE_2D, texture);
      gl.pixelStorei(gl.UNPACK_FLIP_Y_WEBGL, true);
      gl.pixelStorei(gl.UNPACK_PREMULTIPLY_ALPHA_WEBGL, false);
      gl.texImage2D(gl.TEXTURE_2D, 0, gl.RGBA, gl.RGBA, gl.UNSIGNED_BYTE, source);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MIN_FILTER, gl.NEAREST);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_MAG_FILTER, gl.NEAREST);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_S, gl.CLAMP_TO_EDGE);
      gl.texParameteri(gl.TEXTURE_2D, gl.TEXTURE_WRAP_T, gl.CLAMP_TO_EDGE);

      return true;
    } catch (error) {
      //WebGL が無い環境でも画面自体は成立させる。呼び出し側が色付きの丸を描く。
      failed = true;
      gl = null;
      return false;
    }
  }

  /**
   * 塗り替えた画像を data URI で返す。用意できていなければ null。
   */
  function render(color) {
    if (!gl) return null;

    gl.useProgram(program);
    gl.viewport(0, 0, gl.canvas.width, gl.canvas.height);
    gl.disable(gl.BLEND);
    gl.clearColor(0, 0, 0, 0);
    gl.clear(gl.COLOR_BUFFER_BIT);

    gl.activeTexture(gl.TEXTURE0);
    gl.bindTexture(gl.TEXTURE_2D, texture);
    gl.uniform1i(gl.getUniformLocation(program, 'uTex'), 0);
    gl.uniform3fv(gl.getUniformLocation(program, 'uBody'), hexToRgb(color && color.main, [1, 1, 1]));
    gl.uniform3fv(gl.getUniformLocation(program, 'uVisor'), hexToRgb(color && color.visor, [0.58, 0.79, 0.86]));
    gl.uniform3fv(gl.getUniformLocation(program, 'uShadow'), hexToRgb(color && color.shadow, [0.5, 0.5, 0.5]));

    gl.drawArrays(gl.TRIANGLES, 0, 3);

    return gl.canvas.toDataURL('image/png');
  }

  return { init, render };
})();

//別のスクリプトから参照できるよう明示的に公開する。
window.PlayerIcon = PlayerIcon;
