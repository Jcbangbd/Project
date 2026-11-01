生产环境启用算法签名时：

1. 服务器使用Ed25519私钥对原始.py文件字节签名。
2. 将签名Base64放入算法版本信息和下载票据的signature字段。
3. 将对应公钥PEM保存为本目录下algorithm_public_key.pem。
4. 把Assets/StreamingAssets/local-fastapi-config.json中的
   requireAlgorithmSignature改为true。

测试阶段默认false，没有公钥也可以启动，但不具备算法来源真实性保护。
