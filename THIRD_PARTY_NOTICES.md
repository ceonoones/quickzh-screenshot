# 第三方来源与许可范围

项目原创 C# 代码和原创文档使用 MIT。下列组件不因项目的 MIT 许可改变授权；第三方版权声明及许可保留在 OCR 资源内 `licenses/`，发布便携包同时提供该目录。

| 组件 | 来源 | 许可 / 说明 |
| --- | --- | --- |
| RapidOCR-json 0.2.0 | https://github.com/hiroi-sora/RapidOCR-json/releases/tag/v0.2.0 | MIT；原生 CPU 执行器 |
| RapidOcrOnnx、PaddleOCR | https://github.com/RapidAI/RapidOcrOnnx 、https://github.com/PaddlePaddle/PaddleOCR | Apache-2.0 |
| PP-OCRv5 英文 / 东斯拉夫 / 拉丁 ONNX 模型 | https://huggingface.co/monkt/paddleocr-onnx | PaddleOCR 衍生模型，保留 Apache-2.0 及模型来源 |
| PP-OCRv4 定位 / 中文、v3 日语 / 韩语模型 | 随 RapidOCR-json 发布 | PaddleOCR 模型，保留上游许可 |
| ONNX Runtime | https://github.com/microsoft/onnxruntime | MIT 与 ThirdPartyNotices |
| OpenCV | https://github.com/opencv/opencv | Apache-2.0 与其上游声明 |
| Tesseract 5.4.0 Windows 后备引擎 | https://github.com/UB-Mannheim/tesseract/releases/tag/v5.4.0.20240606 | Tesseract Apache-2.0；Windows 构建及依赖见下文 |
| tessdata_fast | https://github.com/tesseract-ocr/tessdata_fast | Apache-2.0；英语等十二种 traineddata |

## Windows 原生依赖

后备 Tesseract 包中的 DLL 来自 UB Mannheim 上游 Windows 构建，未修改；其源代码和构建入口见 https://github.com/UB-Mannheim/tesseract 、https://github.com/tesseract-ocr/tesseract/tree/5.4.0 和 https://github.com/msys2/MINGW-packages 。这些运行库保留各自许可，并非全部 MIT。

随包库包括 leptonica、libarchive、bzip2、Brotli、cairo、datrie、deflate、expat、libffi、fontconfig、FreeType、FriBidi、GCC runtime、giflib、GLib、Graphite2、HarfBuzz、iconv、ICU、gettext、jbigkit、libjpeg-turbo、Lerc、LZ4、XZ、OpenJPEG、OpenSSL、Pango、PCRE2、pixman、libpng、libthai、libtiff、WebP、winpthreads、zlib 与 zstd。组件和许可索引见 `third-party/runtime-sources.json`；其中可能包含 LGPL、GPL 及 GCC Runtime Library Exception 等不同条款。请保留全部原始声明，并按对应库的条款再分发。

资源包内来源清单记录模型与执行器 SHA-256；仓库资源清单固定整个资源 ZIP 的 SHA-256。原生组件未做二进制安全审计，也未宣称全部由本项目源码重建。

## 示例与交互参考

测试样本包含 TOEFL 网站片段，仅用于坐标、字号和图标保留回归验证；第三方文字、商标和插图不属于本项目 MIT 授权，与 TOEFL / ETS 无官方关系。仓库首页使用不含第三方客户端内容的工具栏示意。

工具栏参考 Flameshot 的操作形式，自绘图标与 C# 实现，未复制上游代码或图标。Flameshot 项目见 https://github.com/flameshot-org/flameshot 。
