import socket
import time

def test_server_connection(host, port):
    """测试服务器连接"""
    print(f"正在测试连接 {host}:{port}")
    
    try:
        # 创建 TCP socket
        sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
        # 设置超时时间为 5 秒
        sock.settimeout(5)
        
        # 尝试连接
        start_time = time.time()
        result = sock.connect_ex((host, port))
        end_time = time.time()
        
        if result == 0:
            print(f"✅ 连接成功!")
            print(f"连接耗时: {(end_time - start_time)*1000:.2f}ms")
            
            # 尝试发送一些数据
            try:
                sock.send(b"test")
                print("✅ 数据发送成功")
            except Exception as e:
                print(f"❌ 数据发送失败: {e}")
        else:
            print(f"❌ 连接失败 (错误码: {result})")
            
    except socket.timeout:
        print("❌ 连接超时")
    except Exception as e:
        print(f"❌ 发生错误: {e}")
    finally:
        sock.close()

if __name__ == "__main__":
    # 服务器配置
    SERVER_HOST = "47.120.71.251"  # 你的服务器IP
    SERVER_PORT = 5000             # 你的服务器端口
    
    # 运行测试
    while True:
        test_server_connection(SERVER_HOST, SERVER_PORT)
        print("\n等待 5 秒后重试...")
        time.sleep(5) 