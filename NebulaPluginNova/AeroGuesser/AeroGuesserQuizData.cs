using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Nebula.AeroGuesser;

internal class AeroGuesserQuizData
{
    internal enum Difficulty
    {
        Normal = 0,
        Hard = 1,
    }
    internal record QuizEntry(byte mapId, Difficulty difficulty, Vector2 position, Vector2 viewport);
    internal record QuizRawEntry(Vector2 position, Vector2 viewport)
    {
        static public implicit operator QuizRawEntry((Vector2 position, Vector2 viewport) tuple) => new(tuple.position, tuple.viewport);
    }

    private static QuizRawEntry[] SkeldNormal = [
        //シールド外側
        (new(11.1f, -15.0f), new(2.6f, 2.1f)),
        //シールド(左上)
        (new(8.0f, -11.0f), new(1.3f, 1.3f)),
        //リアクター
        (new(-21.9f, -4.5f), new(1.8f, 1.9f)),
        //コの字ベント
        (new(9.0f, -6.0f), new(1.3f,2f)),
        //コの字カメラ
        (new(13.3f, -3.6f), new(1.2f, 1.2f)),
        //ナビ先端
        (new(18.5f, -4.8f), new(1.6f, 1.4f)),
        //ナビ上のタスク群
        (new(16.5f, -2.5f), new(1.4f, 1f)),
        //O2
        (new(6.7f, -2.45f), new(1.3f, 1.6f)),
        //ウェポンズ
        (new(9.2f, 1.8f), new(1.5f, 1.5f)),
        //カフェボタン
        (new(-0.6f, 1.0f), new(1.2f, 1.3f)),
        //カフェ右上
        (new(2.9f, 5.4f), new(1.3f,1.3f)),
        //メッドベイ
        (new(-7.6f, -0.9f), new(1.7f, 1.9f)),
        //メッドベイ上通路
        (new(-9.0f, 1.8f), new(1.8f,1.0f)),
        //上部エンジン右上
        (new(-15.2f, 2.9f), new(1.4f,1.9f)),
        //セキュ
        (new(-12.5f, -5.2f), new(1.2f, 1.6f)),
        //リアクター下
        (new(-21.4f, -8.4f), new(1.3f,1.7f)),
        //セキュ右上
        (new(-12.3f, -2.9f), new(1f,1f)),
        //セキュ上
        (new(-13.0f, -2.7f), new(1.2f,1.5f)),
        //電気室
        (new(-8.9f, -9.85f), new(2.1f, 1.3f)),
        //電気室左上
        (new(-9.3f, -7.7f), new(1.5f,1.5f)),
        //下部電気間
        (new(-11.7f, -11.1f), new(1.2f,1.7f)),
        //電気ストレージ間
        (new(-6.7f, -13.7f), new(1.8f,1.4f)),
        //ストレージ給油
        (new(-2.5f, -13.6f), new(1.2f,1.2f)),
        //ストレージ
        (new(0.2f, -17.9f), new(1.5f, 3.2f)),
        //アドミンO2端末
        (new(6.5f, -6.2f), new(0.4f, 0.5f)),
        ];

    private static QuizRawEntry[] SkeldHard = [
        //シールド中央
        (new(9.2f, -12.4f), new(1.4f, 1.7f)),
        //コの字
        (new(11.0f, -4.5f), new(1f, 1.8f)),
        //ナビ右下
        (new(17.6f, -6.3f), new(0.9f, 0.9f)),
        //O2下
        (new(7.1f, -4.8f), new(1.7f, 0.8f)),
        //ウェポン左上
        (new(7.5f,3.6f), new(0.75f, 1.3f)),
        //カフェ右上
        (new(2.3f, 3.5f), new(0.9f, 0.9f)),
        //カフェ左上
        (new(-4.0f, 3.3f), new(0.8f,0.8f)),
        //アドミン(上のコンソール)1
        (new(3.7f, -6.5f), new(1f, 1f)),
        //アドミン(上のコンソール)2
        (new(4.6f, -6.5f), new(1f, 1f)),
        //アドミン(上のコンソール)3
        (new(5.7f, -6.5f), new(1f, 1f)),
        //メッドベイ
        (new(-10.4f, -1.7f), new(1.1f, 0.7f)),
        //セキュ
        (new(-14.15f, -3.15f), new(0.64f, 0.84f)),
        //上部エンジン
        (new(-18.2f,2.2f), new(1.3f, 1.8f)),
        //リアクター
        (new(-22.1f, -1.55f), new(0.8f, 0.8f)),
        //下部エンジン
        (new(-18.9f,-9.2f), new(0.9f, 1f)),
        //上部エンジンタスク
        (new(-18.6f, -0.7f), new(2.1f,2.0f)),
        //下部エンジンタスク
        (new(-18.6f, -12.9f), new(2.1f,1.7f)),
        //電気室上
        (new(-6.4f, -8.5f), new(0.9f, 0.7f)),
        //電気室中
        (new(-7.8f, -7.1f), new(1.8f,0.7f)),
        //電気室下
        (new(-7.9f, -11.2f), new(1.3f, 1f)),
        //コミュPC
        (new(1.9f, -15.9f), new(0.8f, 1.2f)),
        //コミュ上
        (new(2.8f, -14.1f), new(1f, 0.8f)),
        //ストレージ
        (new(-3.4f, -9.6f), new(1.1f, 1.1f)),
        //ストレージまわる荷物
        (new(-0.7f, -10.8f), new(0.4f,0.4f)),
        //ストレージ右上
        (new(0.865f, -8.85f), new(0.7f,0.8f)),
        //O2左下
        (new(4.25f, -3.8f), new(0.7f, 0.8f)),
        //O2左上
        (new(5.3f, -2.7f), new(0.8f, 0.8f)),
        //ウェポン右下
        (new(11.1f, 0.4f), new(0.8f, 1.3f)),
        //ナビ先端外側
        (new(19.9f, -4.9f), new(0.8f, 1.2f)),
        //コミュ外側左
        (new(2.7f, -18.7f), new(1.8f, 1.7f)),
        //コミュ外側中
        (new(4.65f, -18.7f), new(2.1f, 1.7f)),
        ];

    private static QuizRawEntry[] MIRANormal = [
        //バルコニー左下
        (new(21.2f, -2.0f), new(1.8f, 0.9f)),
        //バルコニー
        (new(24.2f, -1.4f), new(1.5f, 1.3f)),
        //バルコニーアンテナ
        (new(18.6f, -1.8f), new(1.7f, 1.3f)),
        //ストレージ
        (new(19.1f, 3.0f), new(1f, 1f)),
        //ストレージ右
        (new(20.6f, 3.3f), new(1.2f, 2.0f)),
        //オフィス
        (new(14.8f, 17.7f), new(0.8f, 0.8f)),
        //オフィス上
        (new(15.3f, 21.15f), new(1.6f, 1.2f)),
        //オフィス左
        (new(13.45f, 19.4f), new(1.2f, 1f)),
        //オフィス右
        (new(16.2f, 20.0f), new(0.7f, 0.9f)),
        //グリーンハウス
        (new(14.0f, 22.7f), new(0.8f, 0.8f)),
        //グリーンハウス上
        (new(17.8f, 25.7f), new(1.6f, 2.7f)),
        //アドミン
        (new(21.05f, 19.1f), new(1.2f, 1.2f)),
        //アドミン下
        (new(21.18f, 17.97f), new(0.5f, 0.5f)),
        //コミュ
        (new(16.45f, 5.6f), new(1.22f, 0.89f)),
        //メッドベイ
        (new(16.3f, -0.2f), new(1.3f, 1.8f)),
        //ロッカー
        (new(10.1f, 5.2f), new(1.2f, 1.5f)),
        //ロッカー右下
        (new(10.6f, 1.7f), new(1.0f, 2.8f)),
        //リアクターラボ間
        (new(5.6f, 14.6f), new(0.8f, 1.3f)),
        //リアクター
        (new(2.45f, 13.8f), new(1.4f, 1.4f)),
        //リアクター中央
        (new(2.5f, 12.3f), new(1.7f, 0.9f)),
        //リアクター左下
        (new(0.6f, 11.0f), new(0.95f, 1.8f)),
        //ラボ中
        (new(10.0f, 13.1f), new(0.9f, 1.1f)),
        //ラボ上
        (new(10.1f, 14.6f), new(1.8f, 1.5f)),
        //下通路
        (new(8.3f, -0.5f), new(3.1f, 0.7f)),
        //発射台左
        (new(-8.0f, 2.7f), new(2.5f, 3.6f)),
        //発射台右上外
        (new(-1.2f, 4.5f), new(1.7f, 2.7f)),
        //コミュ上左側
        (new(14.78f, 5.4f), new(0.7f, 1.1f)),
        //コミュ左上
        (new(13.8f, 6.2f), new(0.8f, 0.8f)),
        //コミュ上
        (new(15.6f, 5.6f), new(0.72f, 1.2f)),
        ];
    private static QuizRawEntry[] MIRAHard = [
        //カフェ机左上
        (new(24.2f, 3.2f), new(0.7f, 0.9f)),
        //カフェ机左下
        (new(24.03f, 1.76f), new(0.7f, 0.8f)),
        //カフェ机右下
        (new(26.4f, 1.7f), new(0.9f, 0.9f)),
        //カフェ自販機
        (new(28.4f, 5.6f), new(0.75f, 0.9f)),
        //カフェ左上
        (new(21.7f, 5.8f), new(0.7f, 0.6f)),
        //カフェ三叉路間
        (new(24.0f, 8.0f), new(0.7f, 0.9f)),
        //バルコニー左コンセント
        (new(19.9f, -1.2f), new(0.9f, 1.0f)),
        //ストレージ前
        (new(20.6f, 1.2f), new(0.9f, 0.7f)),
        //ストレージ奥
        (new(20.8f, 5.1f), new(0.45f, 0.52f)),
        //三叉路
        (new(17.8f, 11.2f), new(1.5f, 0.9f)),
        //オフィス
        (new(13.25f, 21.3f), new(0.8f, 0.7f)),
        //オフィス右下
        (new(16.2f, 18.9f), new(1f, 1.55f)),
        //オフィス右上
        (new(16.2f, 21.5f), new(0.5f, 0.6f)),
        //グリーンハウス左
        (new(15.4f, 23.8f), new(0.9f, 0.9f)),
        //グリーンハウス中央
        (new(17.17f, 24.5f), new(0.5f, 0.4f)),
        //アドミン
        (new(19.55f, 21.25f), new(0.68f, 0.77f)),
        //アドミン右上
        (new(22.2f, 20.9f), new(0.9f, 0.9f)),
        //アドミン上
        (new(21.3f, 21.5f), new(1.8f, 0.8f)),
        //アドミン下左
        (new(20.3f, 17.9f), new(0.7f, 0.5f)),
        //コミュ植物
        (new(16.75f, 3.5f), new(0.5f, 0.85f)),
        //メッドベイベッド左
        (new(14.15f, 1.5f), new(0.9f, 1f)),
        //メッドベイベッド中央
        (new(15.5f, 1.5f), new(0.9f, 1f)),
        //メッドベイベッド右
        (new(16.6f, 1.5f), new(0.9f, 1f)),
        //メッドベイ前廊下
        (new(13.28f, -1.2f), new(1.2f, 1.2f)),
        //ロッカー除染
        (new(6.1f, 1.45f), new(1f, 0.85f)),
        //ロッカー中央
        (new(8.8f, 3.8f), new(0.9f, 2.6f)),
        //除染端末下
        (new(7.0f, 2.2f), new(0.5f, 0.5f)),
        //ロッカー左壁(右)
        (new(7.76f, 2.6f), new(0.7f, 0.5f)),
        //除染端末上
        (new(5.1f, 8.9f), new(0.5f, 0.5f)),
        //ラボ荷物
        (new(11.4f, 13.2f), new(0.9f, 0.9f)),
        //ラボ上消火器
        (new(11.8f, 14.7f), new(0.55f, 0.85f)),
        //ラボ右下
        (new(11.9f, 11.0f), new(0.6f, 0.7f)),
        //ラボ下
        (new(9.1f, 11.3f), new(1.3f, 0.7f)),
        //リアクター左
        (new(0.5f, 12.4f), new(0.55f, 0.8f)),
        //リアクター右上
        (new(4.5f, 14.9f), new(0.8f, 1.3f)),
        //リアクター左上
        (new(0.5f, 14.9f), new(0.8f, 1.3f)),
        //下通路植物
        (new(7.1f, -0.4f), new(0.7f, 0.5f)),
        //下通路ロゴ
        (new(5.0f, -0.3f), new(0.7f, 0.3f)),
        //発射台
        (new(-4.5f, 2.6f), new(1.5f, 4f)),
        //発射台左下
        (new(-6.4f, 0.8f), new(1.0f, 1.2f)),
        ];

    private static QuizRawEntry[] PolusNormal = [
        //ウェポン中
        (new(10.9f, -24.6f), new(2.7f, 1.5f)),
        //ウェポン外
        (new(8.9f, -21.6f), new(1.1f, 1.4f)),
        //アドミン
        (new(21.6f, -24.4f), new(1.8f, 1.2f)),
        //オフィス
        (new(22.5f, -17.0f), new(0.8f, 0.8f)),
        //ラボトイレ
        (new(33.9f, -9.2f), new(1.3f, 0.9f)),
        //望遠鏡
        (new(33.88f, -4.9f), new(1f, 1.6f)),
        //ドリル(穴)
        (new(26.63f, -7.6f), new(1.2f, 1f)),
        //ストレージ
        (new(21.45f, -11.0f), new(1.4f, 1.1f)),
        //スペシメン
        (new(36.58f, -20.0f), new(2.8f, 1.2f)),
        //通信アンテナ
        (new(13.6f, -13.7f), new(1f, 1.9f)),
        //大岩
        (new(26.5f, -13.4f), new(3.8f, 2.2f)),
        //セキュ
        (new(2.0f, -9.5f), new(0.8f, 1.3f)),
        //O2木
        (new(1.1f, -15.3f), new(1.0f, 1.5f)),
        //O2中央
        (new(4.9f, -20.2f), new(3.2f, 2.4f)),
        ];
    private static QuizRawEntry[] PolusHard = [
        //ウェポン
        (new(13.6f, -24.8f), new(0.9f, 0.9f)),
        //ウェポンオフィス間
        (new(18.2f, -25.4f), new(1.4f, 0.68f)),
        //アドミン
        (new(21.9f, -21.9f), new(0.6f, 0.8f)),
        //オフィス通路
        (new(19.8f,-18.42f), new(1.6f, 0.45f)),
        //ラボトイレ左
        (new(33.1f, -10.3f), new(0.8f, 0.8f)),
        //ラボトイレ右
        (new(35.0f, -9.0f), new(1.1f, 0.6f)),
        //ラボアニメーション
        (new(30.25f, -6.65f), new(0.72f, 0.35f)),
        //ラボ左側
        (new(29.1f, -8.0f), new(0.38f, 0.65f)),
        //ラボ左端ポスター
        (new(25.3f, -8.9f), new(0.5f, 0.5f)),
        //ドリル
        (new(27.9f, -5.2f), new(0.8f, 0.6f)),
        //ドリル左
        (new(25.5f, -6.7f), new(1.2f, 0.4f)),
        //ドロップシップ外左
        (new(12.4f, -5.2f), new(0.9f, 0.8f)),
        //ドロップシップ外右
        (new(21.0f, -5.2f), new(0.9f, 0.8f)),
        //ストレージ
        (new(19.4f, -10.5f), new(1.2f, 0.5f)),
        //スペシメン右
        (new(39.1f, -20.55f), new(0.5f, 0.9f)),
        //スペシメン左
        (new(34.2f, -19.3f), new(0.65f, 0.65f)),
        //スペシメン外
        (new(31.2f, -23.3f), new(2.3f, 1.2f)),
        //セキュ
        (new(4.1f, -10.9f), new(0.5f, 0.8f)),
        //O2窓
        (new(3.77f, -15.2f), new(1.2f, 0.8f)),
        //O2配管
        (new(0.58f, -18.7f), new(0.44f, 1.2f)),
        //ボイラー壁
        (new(1.9f, -22.9f), new(0.9f, 0.5f)),
        //ボイラー左タンク
        (new(0.62f, -23.1f), new(0.58f, 0.8f)),
        //ボイラー床
        (new(2.3f, -24.4f), new(1.2f, 0.8f)),
        //コミュ机
        (new(11.35f, -16.7f), new(1f, 0.8f)),
        ];

    private static QuizRawEntry[] AirshipNormal = [
        //金庫絵画上
        (new(-9.6f, 13.45f), new(1.5f, 1.5f)),
        //金庫絵画右上
        (new(-5.9f, 12.7f), new(1.3f, 1.2f)),
        //金庫左下
        (new(-10.4f, 6.9f), new(1.5f, 1.5f)),
        //金庫左上
        (new(-10.6f, 10.3f), new(2.7f, 3.6f)),
        //ミーティング左
        (new(5.4f, 16.3f), new(0.9f, 1f)),
        //ミーティング
        (new(11.0f, 15.1f), new(1f, 1f)),
        //ミーティング右
        (new(17.1f, 15.4f), new(1.5f, 1.2f)),
        //昇降機右
        (new(10.7f, 7.0f), new(1.9f, 1.3f)),
        //昇降機排気口
        (new(14.5f, 8.0f), new(1f, 1f)),
        //エンジンクレーン
        (new(1.5f, 1.5f), new(2.8f, 2.5f)),
        //エンジン下給油
        (new(-1.7f, -2.4f), new(1f, 1f)),
        //エンジン上給油
        (new(-6.5f, 0.5f), new(1f, 1f)),
        //コミュ端末
        (new(-14.1f, 2.6f), new(1.5f, 1.2f)),
        //コックピットチェア
        (new(-19.0f, -0.6f), new(1f, 1f)),
        //コックピットアドミン
        (new(-23.5f, 0.2f), new(1f, 1f)),
        //ウェポン上
        (new(-12.2f, -2.3f), new(1.3f, 0.8f)),
        //ウェポン左下
        (new(-14.4f, -7.6f), new(1.6f, 1f)),
        //キッチンまないた
        (new(-4.6f, -10.2f), new(1.5f, 1f)),
        //キッチン野菜
        (new(-4.2f, -8.4f), new(1.5f, 1f)),
        //展望
        (new(-13.0f, -15.2f), new(1.4f, 1.2f)),
        //ポートレート
        (new(4.1f,-11.7f), new(1.9f, 1.2f)),
        //セキュカメラ
        (new(8.08f, -9.8f), new(1.8f, 1.3f)),
        //セキュ展望
        (new(5.1f, -14.4f), new(3f, 1f)),
        //電気上
        (new(17.0f, -5.5f), new(1.6f, 1f)),
        //メイン宝箱
        (new(6.6f, 3.6f), new(0.9f, 0.9f)),
        //メイン掃除道具
        (new(10.0f, 3.6f), new(1.4f, 1f)),
        //メイン暗室
        (new(13.0f, 2.9f), new(1f, 1f)),
        //タオルタスク派生先
        (new(18.4f, 5.1f), new(1.2f, 1.2f)),
        //アーカイブ中央
        (new(19.9f, 9.3f), new(1.8f, 1f)),
        //ラウンジ上
        (new(26.3f, 10.4f), new(1.8f, 0.65f)),
        //トイレごみ箱
        (new(28.5f, 6.1f), new(1.5f, 1.2f)),
        //ロミジュリ
        (new(27.4f, 0.0f), new(2.1f, 3f)),
        //貨物金庫
        (new(36.7f, -2.7f), new(1.7f, 1.2f)),
        //貨物給油
        (new(38.6f, 1.8f), new(2f, 1f)),
        //医務室レントゲン
        (new(25.5f, -4.8f), new(1f, 1f)),
        //医務室体重計
        (new(22.2f, -5.4f), new(1.8f, 1.8f)),
        ];
    private static QuizRawEntry[] AirshipHard = [
        //金庫マネキン上
        (new(-7.42f, 10.6f), new(0.7f, 0.8f)),
        //金庫マネキン上中より
        (new(-6.5f, 9.8f), new(0.7f, 0.8f)),
        //金庫マネキン下中より
        (new(-6.7f, 7.3f), new(0.7f, 0.8f)),
        //金庫左上
        (new(-13.0f, 11.4f), new(0.6f, 0.6f)),
        //金庫右
        (new(-4.85f, 9.9f), new(0.9f, 0.8f)),
        //宿舎左
        (new(-2.3f, 9.5f), new(1f, 1f)),
        //ミーティングエレベータ
        (new(3.9f, 16.4f), new(1f, 0.7f)),
        //ミーティング左
        (new(6.8f, 16.3f), new(1f, 1f)),
        //ミーティング右
        (new(15.3f, 16.2f), new(1.1f, 0.9f)),
        //セキュエレベータ
        (new(5.7f, -9.6f), new(1f, 0.8f)),
        //えんぴつ
        (new(10.8f, 9.5f), new(0.8f, 0.45f)),
        //エンジンパイプ上
        (new(-2.5f, 2.2f), new(1.3f, 0.9f)),
        //エンジン左
        (new(-8.1f, 1.0f), new(0.8f, 1.2f)),
        //コミュ中央
        (new(-13.0f, 2.3f), new(0.7f, 1f)),
        //コミュ右上
        (new(-12.3f, 3.5f), new(0.4f, 0.6f)),
        //コックピット上1
        (new(-19.4f, 1.4f), new(0.4f, 0.4f)),
        //コックピット上2
        (new(-18.8f, 1.4f), new(0.4f, 0.4f)),
        //コックピット上3
        (new(-19.4f, 1.4f), new(0.4f, 0.4f)),
        //コックピット上4
        (new(-17.9f, 1.4f), new(0.4f, 0.4f)),
        //ウェポン左上
        (new(-14.5f, -2.8f), new(0.9f, 1.1f)),
        //ウェポン左下
        (new(-15.1f, -7.6f), new(0.5f, 0.4f)),
        //ウェポン右上
        (new(-9.2f, -2.7f), new(0.5f, 0.4f)),
        //キッチン上
        (new(-7.0f, -5.5f), new(0.8f, 0.6f)),
        //キッチン右1
        (new(-3.2f, -8.4f), new(0.7f, 0.8f)),
        //キッチン右2
        (new(-2.5f, -8.4f), new(0.7f, 0.8f)),
        //キッチン下
        (new(-4.6f, -11.9f), new(0.6f, 0.5f)),
        //展望廊下
        (new(-12.8f, -12.0f), new(3.6f, 0.8f)),
        //ポートレート1
        (new(-0.5f,-11.3f), new(0.6f, 0.7f)),
        //ポートレート2
        (new(0.81f,-11.3f), new(0.6f, 0.7f)),
        //ポートレート3
        (new(2.15f,-11.3f), new(0.6f, 0.7f)),
        //ポートレート4
        (new(3.55f,-11.3f), new(0.6f, 0.7f)),
        //セキュDVD右
        (new(8.15f, -11.5f), new(1.2f, 1.2f)),
        //セキュDVD左
        (new(6.1f, -11.5f), new(1.2f, 1.2f)),
        //セキュ展望入り口
        (new(8.18f, -13.9f), new(0.8f, 0.8f)),
        //電気壁内左上
        (new(14.8f, -7.1f), new(2.5f, 2f)),
        //電気壁内右上
        (new(17.9f, -7.1f), new(2.5f, 2f)),
        //電気壁内右下
        (new(17.9f, -9.5f), new(2.5f, 2f)),
        //電気左
        (new(10.3f, -5.5f), new(1.5f, 0.9f)),
        //電気アスタリスク壁
        (new(18.7f, -3.3f), new(1.1f, 0.8f)),
        //電気左下
        (new(11.2f, -7.7f), new(0.45f, 0.45f)),
        //メイン掃除道具
        (new(8.6f, 2.8f), new(1.1f, 1f)),
        //メイン右
        (new(16.7f, 2.8f), new(0.8f, 1f)),
        //メイン中央
        (new(11.1f, 0.6f), new(0.8f, 0.8f)),
        //シャワー蛇口
        (new(19.3f, 1.6f), new(0.6f, 0.7f)),
        //シャワー中央
        (new(21.0f, 0.1f), new(0.6f, 0.4f)),
        //ビリヤード上
        (new(25.9f, 8.4f), new(1.3f, 2.2f)),
        //ビリヤード下
        (new(25.9f, 6.0f), new(1.3f, 2.2f)),
        //トイレ1
        (new(33.66f, 7.5f), new(1.05f, 1.9f)),
        //トイレ2
        (new(32.43f, 7.5f), new(1.05f, 1.9f)),
        //トイレ3
        (new(30.8f, 7.5f), new(1.05f, 1.9f)),
        //トイレ4
        (new(29.3f, 7.5f), new(1.05f, 1.9f)),
        //換気口左上ファン
        (new(26.2f, 2.0f), new(0.9f, 0.9f)),
        //換気口右上ファン
        (new(28.9f, 1.5f), new(0.9f, 1.8f)),
        //換気口左下ファン
        (new(26.4f, -2.3f), new(1.3f, 1.9f)),
        //貨物下帽子1
        (new(33.9f, -3.3f), new(1f, 0.9f)),
        //貨物下帽子2
        (new(34.7f, -2.2f), new(0.7f, 0.7f)),
        //貨物下帽子3
        (new(36.9f, -1.6f), new(0.7f, 0.7f)),
        //貨物下帽子4
        (new(38.2f, -1.8f), new(0.7f, 0.7f)),
        //貨物下帽子5
        (new(38.8f, -3.6f), new(0.7f, 0.7f)),
        //医務室給水機
        (new(28.9f, -4.7f), new(0.9f, 0.6f)),
        //医務室ファイル
        (new(23.5f, -4.8f), new(0.9f, 0.7f)),
        //医務室雑誌
        (new(29.3f, -7.0f), new(1f, 0.6f)),
        ];

    private static QuizRawEntry[] FunleNormal = [
        //採掘場中央
        (new(13.3f,8.4f), new(1.5f, 1.5f)),
        //採掘場左
        (new(10.8f, 9.1f), new(1f, 1.5f)),
        //採掘場右上
        (new(14.4f, 10.3f), new(1.1f, 1.1f)),
        //下部エンジン
        (new(23.4f, 3.0f), new(1.4f, 1.4f)),
        //下部エンジン
        (new(24.5f, 5.3f), new(2.5f, 2.5f)),
        //展望台下
        (new(8.1f, 0.0f), new(1f, 0.9f)),
        //ストレージ上
        (new(0.5f, 7.3f), new(1.5f, 1f)),
        //ストレージ側ジップライン
        (new(3.22f, 7.4f), new(0.7f, 0.7f)),
        //コミュ旗
        (new(18.1f, 16.3f), new(1.1f, 1.1f)),
        //コミュアンテナ
        (new(22.4f, 16.8f), new(2.1f, 3.2f)),
        //ドロップシップ上
        (new(-10.5f, 13.7f), new(1.2f, 1.3f)),
        //ドロップシップ中央
        (new(-9.1f, 10.6f), new(1.4f, 1f)),
        //カフェ左上 (難しいかも？)
        (new(-18.3f, 7.8f), new(2f, 1f)),
        //カフェテーブル
        (new(-15.7f, 6.1f), new(2.6f, 1.5f)),
        //スプラッシュ筋トレ
        (new(-19.15f, 0.7f), new(1.25f, 1.05f)),
        //スプラッシュ浮き輪
        (new(-20.3f, -1.4f), new(1.5f, 1.5f)),
        //スプラッシュシャワー
        (new(-16.75f, 1.3f), new(2.1f, 1.2f)),
        //キッチン右上
        (new(-12.8f, -7.7f), new(0.8f, 1f)),
        //キッチン右下
        (new(-12.7f, -9.2f), new(0.8f, 1f)),
        //ジャングル砂浜間(左)
        (new(-9.0f, -9.3f), new(3.1f, 1.7f)),
        //研究室左
        (new(-6.15f, -9.5f), new(0.68f, 0.9f)),
        //宿舎右
        (new(3.3f, -0.78f), new(0.9f, 0.8f)),
        //ミーティング中央
        (new(-3.0f, -1.1f), new(1.3f, 1.3f)),
        //ミーティング左
        (new(-5.47f, -0.9f), new(0.75f, 0.9f)),
        //ジャングル中央
        (new(3.1f, -12.2f), new(5.8f, 4.2f)),
        //ジャングル右方
        (new(15.7f, -12.2f), new(7f, 4f)),
        //温室左上
        (new(8.3f, -9.3f), new(1.3f, 0.8f)),
        //ジャングル右上
        (new(15.0f, -6.2f), new(1.4f, 2f)),
        //リアクター右
        (new(23.3f, -6.5f), new(1.5f, 1.2f)),
        //リアクター
        (new(23.7f, -5.2f), new(2.1f, 2.7f)),
        //高台O2の残骸
        (new(16.9f, 1.84f), new(1.2f, 1.9f)),
        //キャンプファイア
        (new(-9.7f, 1.5f), new(1.9f, 2f)),
        //キャンプファイア右上
        (new(-8.2f, 2.9f), new(1.3f, 1f)),
        ];
    private static QuizRawEntry[] FungleHard = [
        //採掘場左下
        (new(10.9f, 7.6f), new(1f, 1f)),
        //採掘場右
        (new(14.4f, 8.6f), new(0.85f, 1.5f)),
        //採掘場前右
        (new(16.3f, 5.2f), new(1f, 0.9f)),
        //高台梯子中
        (new(18.4f, 7.7f), new(1.6f, 0.9f)),
        //縄の取っ手上方
        (new(17.7f, 10.9f), new(1.3f, 1.5f)),
        //縄の取っ手下方
        (new(14.7f, -1.4f), new(1.2f, 1.2f)),
        //展望台上
        (new(9.2f, 5.3f), new(1f, 0.8f)),
        //ストレージ左上
        (new(-1.25f, 6.95f), new(0.68f, 1.1f)),
        //ジップライン中
        (new(7.7f, 8.9f), new(1.6f, 2.6f)),
        //ジップライン中　上側
        (new(13.1f, 15.8f), new(3.6f, 1.8f)),
        //コミュ右
        (new(24.9f, 12.0f), new(0.8f, 1.3f)),
        //コミュ中
        (new(23.0f, 13.6f), new(1.96f, 0.75f)),
        //コミュ右上
        (new(24.9f, 14.8f), new(0.8f, 0.6f)),
        //ドロップシップ左下
        (new(-10.1f, 8.3f), new(1.4f, 2.5f)),
        //カフェ上
        (new(-14.8f, 7.9f), new(0.9f, 1.2f)),
        //スプラッシュラジカセ
        (new(-18.39f, -1.6f), new(0.9f, 0.9f)),
        //スプラッシュロッカー
        (new(-17.2f, -1.8f), new(0.8f, 0.9f)),
        //スプラッシュ右上
        (new(-14.9f, 0.8f), new(1.1f, 0.9f)),
        //スプラッシュ左端
        (new(-21.7f, 1.5f), new(1.05f, 0.8f)),
        //キッチン左
        (new(-18.4f, -9.1f), new(0.6f, 0.8f)),
        //キッチン左上
        (new(-17.18f, -6.0f), new(0.5f, 0.7f)),
        //マップ左下
        (new(-17.3f, -15.6f), new(3f, 3f)),
        //研究室左上
        (new(-5.6f, -7.5f), new(0.9f, 0.7f)),
        //研究室右上
        (new(-3.0f, -7.6f), new(0.9f, 1f)),
        //ジャングル砂浜間(右)
        (new(-2.5f, -4.0f), new(1.3f, 0.9f)),
        //宿舎上
        (new(1.75f, -0.3f), new(1.1f, 0.6f)),
        //宿舎右
        (new(3.1f, -1.7f), new(1f, 0.7f)),
        //ミーティング上
        (new(-2.9f, 0.6f), new(0.9f, 0.57f)),
        //ジャングル下方
        (new(8.94f, -14.7f), new(1.65f, 1.0f)),
        //温室右下
        (new(10.5f, -13.1f), new(1f, 1f)),
        //温室右
        (new(10.7f, -10.8f), new(1f, 0.85f)),
        //温室左
        (new(7.7f, -10.8f), new(1f, 0.85f)),
        //温室中央
        (new(9.7f, -10.8f), new(1f, 0.85f)),
        //ジャングル上方
        (new(5.0f, -5.5f), new(1.3f, 1f)),
        //リアクター上
        (new(21.64f, -5.6f), new(0.75f, 0.45f)),
        //リアクター上端
        (new(20.9f, -4.6f), new(1f, 0.7f)),
        //リアクター床
        (new(21.7f, -7.5f), new(0.9f, 0.9f)),
        //リアクター左下
        (new(20.8f, -8.9f), new(1f, 0.9f)),
        //高台左下の宝石
        (new(13.0f, 0.0f), new(1.95f, 1.45f)),
        //砂浜下方の障壁
        (new(-11.3f, -3.2f), new(2f, 1.5f)),
        //キャンプファイア右下
        (new(-8.4f, 0.5f), new(1.5f, 1f)),
        ];

    private static QuizRawEntry[][][] AllEntries = [
        [SkeldNormal, SkeldHard],
        [MIRANormal, MIRAHard],
        [PolusNormal, PolusHard],
        [[],[]],
        [AirshipNormal, AirshipHard],
        [FunleNormal, FungleHard],
        ];

    internal static QuizEntry[] GetAllEntry()
    {
        List<QuizEntry> entries = [];
        for (int i1 = 0; i1 < AllEntries.Length; i1++)
        {
            if (i1 != 1) continue;
            for (int i2 = 0; i2 < AllEntries[i1].Length; i2++)
            {
                entries.AddRange(AllEntries[i1][i2].Select(entry => new QuizEntry((byte)i1, (Difficulty)i2, entry.position, entry.viewport)));
            }
        }
        return entries.ToArray();
    }

    internal static QuizEntry[] GetRandomEntry(int num, float[] mapWeight, float[] difficultyWeight)
    {
        List<(byte mapId, int difficulty, float weight, int used, int[] randomArray)> categoriesList = [];
        float weightSum = 0f;

        for(int i1 = 0;i1 < AllEntries.Length; i1++)
        {
            if (mapWeight.Length <= i1) continue;
            if (!(mapWeight[i1] > 0f)) continue;
            
            for(int i2 = 0;i2 < AllEntries[i1].Length; i2++)
            {
                if (difficultyWeight.Length <= i2) continue;
                if (!(difficultyWeight[i2] > 0f)) continue;

                int count = AllEntries[i1][i2].Length;
                if (count == 0) continue;

                float weight = mapWeight[i1] * difficultyWeight[i2];
                weightSum += weight;
                categoriesList.Add(((byte)i1, i2, weight, 0, Helpers.GetRandomArray(count)));
            }
        }

        if (categoriesList.Count == 0) return GetRandomEntry(num, [1f, 1f, 1f, 0f, 1f, 1f], [1f, 1f]);

        var categories = categoriesList.ToArray();
        List<QuizEntry> entries = [];

        for (int trial = 0; trial < num; trial++)
        {
            float val = System.Random.Shared.NextSingle() * weightSum;

            int cIndex = categoriesList.Count - 1;
            for (int i = 0; i < categoriesList.Count; i++)
            {
                val -= categoriesList[i].weight;
                if (val < 0f)
                {
                    cIndex = i;
                    break;
                }
            }

            var copied = categories[cIndex];
            int internalIndex = copied.used;
            categories[cIndex].used++;
            var selected = AllEntries[copied.mapId][copied.difficulty][copied.randomArray[internalIndex]];
            entries.Add(new(copied.mapId, (Difficulty)copied.difficulty, selected.position, selected.viewport));
        }

        return entries.ToArray();
    }
}
