using System;
using System.Collections;
using System.Net.Sockets;
using System.Threading;

using Unity.Collections;
using UnityEngine;
using UnityEngine.Rendering;


/*
+=====================================
 ファイル名 : CS_MayaPreviewFrameSender
 概要       : Maya Preview用HDRPフレーム送信
              Playerに表示された最終画面を
              必要な時だけキャプチャしてMayaへ送る
 作者       : ヨシモト リョウ
 履歴       : 2026/09/14 新規作成
=====================================+
*/

public class CS_MayaPreviewFrameSender : MonoBehaviour
{
    // =========================================================
    // Maya送信先
    // =========================================================

    private const string MAYA_HOST =
        "127.0.0.1";

    private const int MAYA_FRAME_PORT =
        50002;


    // =========================================================
    // Capture用RenderTexture
    // =========================================================

    private RenderTexture _captureRenderTexture;


    // =========================================================
    // 状態
    // =========================================================

    // GPU Readback中
    private bool _isReadbackRunning = false;

    // 次フレームCapture予約済み
    private bool _captureRequested = false;


    // =========================================================
    // Start
    // =========================================================

    private void Start()
    {
        Application.runInBackground =
            true;


        if (!SystemInfo.supportsAsyncGPUReadback)
        {
            Debug.LogError(
                "Maya Preview FrameSender : " +
                "AsyncGPUReadback非対応"
            );

            return;
        }


        Debug.Log(
            "Maya Preview FrameSender : 初期化成功\n" +
            "Main Camera TargetTexture不要\n" +
            "Player最終画面をCaptureします。"
        );
    }


    // =========================================================
    // RuntimeServerから呼ぶ
    //
    // Mesh更新
    // ↓
    // HDRP描画
    // ↓
    // 1枚だけCapture
    // =========================================================

    public void RequestFrameAfterRender()
    {
        if (_captureRequested)
        {
            return;
        }


        _captureRequested =
            true;


        StartCoroutine(
            CaptureAfterRender()
        );
    }


    // =========================================================
    // HDRP描画完了待ち
    // =========================================================

    private IEnumerator CaptureAfterRender()
    {
        // =====================================================
        // 現在のUpdateでMeshが変更されている
        //
        // 次のフレーム終端まで待つことで
        // 新しいMeshをHDRPが描画した後にCaptureする
        // =====================================================

        yield return new WaitForEndOfFrame();


        _captureRequested =
            false;


        CaptureCurrentFrame();
    }


    // =========================================================
    // 現在のPlayer画面をCapture
    // =========================================================

    private void CaptureCurrentFrame()
    {
        if (_isReadbackRunning)
        {
            return;
        }


        int width =
            Screen.width;

        int height =
            Screen.height;


        if (
            width <= 0 ||
            height <= 0
        )
        {
            Debug.LogError(
                "Maya Preview FrameSender : " +
                "Screen Sizeが不正です。"
            );

            return;
        }


        // =====================================================
        // Windowサイズが変わったらRTを作り直す
        // =====================================================

        EnsureCaptureRenderTexture(
            width,
            height
        );


        if (_captureRenderTexture == null)
        {
            return;
        }


        _isReadbackRunning =
            true;


        // =====================================================
        // ★重要
        //
        // Main Camera.targetTextureではなく、
        // 実際にPlayer Windowへ表示された
        // 最終フレームを取得する
        //
        // HDRP
        // Lighting
        // Exposure
        // Tonemapping
        // PostProcess
        //
        // を通った最終表示結果
        // =====================================================

        ScreenCapture.CaptureScreenshotIntoRenderTexture(
            _captureRenderTexture
        );


        // =====================================================
        // GPU → CPU
        // =====================================================

        AsyncGPUReadback.Request(
            _captureRenderTexture,
            0,
            TextureFormat.RGBA32,
            OnReadbackComplete
        );
    }


    // =========================================================
    // Capture RT準備
    // =========================================================

    private void EnsureCaptureRenderTexture(
        int width,
        int height
    )
    {
        // 同じサイズならそのまま使用
        if (
            _captureRenderTexture != null &&
            _captureRenderTexture.width == width &&
            _captureRenderTexture.height == height
        )
        {
            return;
        }


        // =====================================================
        // 古いRT削除
        // =====================================================

        ReleaseCaptureRenderTexture();


        // =====================================================
        // 新規作成
        //
        // Player最終表示用なのでsRGB
        // =====================================================

        _captureRenderTexture =
            new RenderTexture(
                width,
                height,
                0,
                RenderTextureFormat.ARGB32,
                RenderTextureReadWrite.sRGB
            );


        _captureRenderTexture.name =
            "RT_MayaPreview_ScreenCapture";


        _captureRenderTexture.filterMode =
            FilterMode.Bilinear;


        _captureRenderTexture.wrapMode =
            TextureWrapMode.Clamp;


        _captureRenderTexture.Create();


        Debug.Log(
            "Maya Preview FrameSender : " +
            "Capture RT作成\n" +
            width +
            " x " +
            height
        );
    }


    // =========================================================
    // Readback完了
    // =========================================================

    private void OnReadbackComplete(
        AsyncGPUReadbackRequest request
    )
    {
        _isReadbackRunning =
            false;


        if (request.hasError)
        {
            Debug.LogError(
                "Maya Preview FrameSender : " +
                "GPU Readback失敗"
            );

            return;
        }


        if (_captureRenderTexture == null)
        {
            return;
        }


        // =====================================================
        // RGBA取得
        // =====================================================

        NativeArray<byte> rawData =
            request.GetData<byte>();


        byte[] frameData =
            rawData.ToArray();


        int width =
            _captureRenderTexture.width;


        int height =
            _captureRenderTexture.height;


        Debug.Log(
            "Maya Preview FrameSender : " +
            "Player Frame取得成功\n" +
            "Resolution : " +
            width +
            " x " +
            height +
            "\nRGBA Bytes : " +
            frameData.Length
        );


        // =====================================================
        // Maya送信
        // =====================================================

        ThreadPool.QueueUserWorkItem(
            _ =>
            {
                SendFrameToMaya(
                    width,
                    height,
                    frameData
                );
            }
        );
    }


    // =========================================================
    // MayaへFrame送信
    // =========================================================

    private void SendFrameToMaya(
        int width,
        int height,
        byte[] frameData
    )
    {
        try
        {
            using TcpClient client =
                new TcpClient();


            client.Connect(
                MAYA_HOST,
                MAYA_FRAME_PORT
            );


            using NetworkStream stream =
                client.GetStream();


            using System.IO.BinaryWriter writer =
                new System.IO.BinaryWriter(
                    stream
                );


            // =================================================
            // Header
            //
            // width
            // height
            // dataSize
            //
            // int32 × 3
            // =================================================

            writer.Write(
                width
            );


            writer.Write(
                height
            );


            writer.Write(
                frameData.Length
            );


            // =================================================
            // RGBA
            // =================================================

            writer.Write(
                frameData
            );


            writer.Flush();


            Debug.Log(
                "Maya Preview FrameSender : " +
                "MayaへFrame送信成功"
            );
        }
        catch (Exception exception)
        {
            Debug.LogError(
                "Maya Preview FrameSender : " +
                "MayaへのFrame送信失敗\n" +
                exception
            );
        }
    }


    // =========================================================
    // Capture RT解放
    // =========================================================

    private void ReleaseCaptureRenderTexture()
    {
        if (_captureRenderTexture == null)
        {
            return;
        }


        if (_captureRenderTexture.IsCreated())
        {
            _captureRenderTexture.Release();
        }


        Destroy(
            _captureRenderTexture
        );


        _captureRenderTexture =
            null;
    }


    // =========================================================
    // Destroy
    // =========================================================

    private void OnDestroy()
    {
        ReleaseCaptureRenderTexture();
    }
}