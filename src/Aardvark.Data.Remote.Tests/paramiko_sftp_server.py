#!/usr/bin/env python3
"""
Robust SFTP test server using Paramiko for Aardvark.Data.Remote testing
"""
import os
import sys
import socket
import threading
import argparse
import json
import time
import logging
from pathlib import Path

try:
    import paramiko
    from paramiko import ServerInterface, SFTPServerInterface, SFTPServer, SFTPAttributes, SFTPHandle, SFTP_OK
    from paramiko.sftp import SFTP_OK, SFTP_FAILURE, SFTP_NO_SUCH_FILE, SFTP_PERMISSION_DENIED
except ImportError:
    print("ERROR: paramiko not installed. Run: pip install paramiko")
    sys.exit(1)

# Configure logging
logging.basicConfig(level=logging.INFO, format='[%(levelname)s] %(message)s')
logger = logging.getLogger(__name__)

class TestSFTPHandle(SFTPHandle):
    """Handle for SFTP file operations"""
    def __init__(self, flags=0):
        super().__init__(flags)
        self.file_obj = None
        
    def close(self):
        if self.file_obj:
            self.file_obj.close()
        return SFTP_OK
    
    def read(self, offset, length):
        if not self.file_obj:
            return SFTP_FAILURE
        try:
            self.file_obj.seek(offset)
            data = self.file_obj.read(length)
            # Return the data bytes, or empty bytes if EOF
            return data if isinstance(data, bytes) else b''
        except Exception as e:
            logger.error(f"Read error: {e}")
            return SFTP_FAILURE
    
    def write(self, offset, data):
        if not self.file_obj:
            return SFTP_FAILURE
        try:
            self.file_obj.seek(offset)
            self.file_obj.write(data)
            return SFTP_OK
        except Exception as e:
            logger.error(f"Write error: {e}")
            return SFTP_FAILURE
    
    def stat(self):
        try:
            return SFTPAttributes.from_stat(os.fstat(self.file_obj.fileno()))
        except:
            return SFTP_FAILURE

class TestSFTPServer(SFTPServerInterface):
    """SFTP server implementation"""
    def __init__(self, server, *args, **kwargs):
        # SFTPServerInterface expects only the server parameter
        super().__init__(server)
        # Extract our custom root parameter
        self.root = kwargs.get('root', os.path.join(os.path.expanduser('~'), 'sftp_test'))
        os.makedirs(self.root, exist_ok=True)
        logger.info(f"SFTP root directory: {self.root}")
    
    def _realpath(self, path):
        """Convert SFTP path to real filesystem path"""
        # Remove leading slashes and normalize
        path = path.replace('\\', '/')
        while path.startswith('/'):
            path = path[1:]
        
        # Handle empty path as root
        if not path:
            return self.root
        
        # Join with root and normalize
        real_path = os.path.normpath(os.path.join(self.root, path))
        
        # Security check: ensure path is within root
        if not real_path.startswith(self.root):
            raise PermissionError(f"Access denied: {path}")
        
        return real_path
    
    def list_folder(self, path):
        """List directory contents"""
        logger.info(f"LIST: {path}")
        try:
            real_path = self._realpath(path)
            if not os.path.exists(real_path):
                return SFTP_NO_SUCH_FILE
            if not os.path.isdir(real_path):
                return SFTP_FAILURE
            
            items = []
            for name in os.listdir(real_path):
                item_path = os.path.join(real_path, name)
                try:
                    attr = SFTPAttributes.from_stat(os.stat(item_path))
                    attr.filename = name
                    items.append(attr)
                except:
                    pass  # Skip items we can't stat
            
            return items
        except Exception as e:
            logger.error(f"List folder error: {e}")
            return SFTP_FAILURE
    
    def open(self, path, flags, attr):
        """Open a file"""
        logger.info(f"OPEN: {path} (flags={flags})")
        try:
            real_path = self._realpath(path)
            logger.info(f"Real path: {real_path}")
            
            # Check if file exists for read operations
            if flags == 0 or not (flags & (os.O_WRONLY | os.O_RDWR)):
                if not os.path.exists(real_path):
                    logger.error(f"File not found: {real_path}")
                    return SFTP_NO_SUCH_FILE
            
            # Determine file mode from flags
            binary_flag = getattr(os, 'O_BINARY', 0)
            flags |= binary_flag
            
            if flags & os.O_WRONLY:
                if flags & os.O_APPEND:
                    mode = 'ab'
                else:
                    mode = 'wb'
            elif flags & os.O_RDWR:
                if flags & os.O_APPEND:
                    mode = 'a+b'
                else:
                    mode = 'r+b'
            else:
                mode = 'rb'
            
            logger.info(f"Opening file with mode: {mode}")
            
            # Ensure directory exists for write operations
            if 'w' in mode or 'a' in mode:
                os.makedirs(os.path.dirname(real_path), exist_ok=True)
            
            file_obj = open(real_path, mode)
            handle = TestSFTPHandle(flags)
            handle.file_obj = file_obj
            handle.filename = real_path
            
            logger.info(f"File opened successfully: {real_path}")
            return handle
            
        except FileNotFoundError as e:
            logger.error(f"File not found: {e}")
            return SFTP_NO_SUCH_FILE
        except PermissionError as e:
            logger.error(f"Permission denied: {e}")
            return SFTP_PERMISSION_DENIED
        except Exception as e:
            logger.error(f"Open error: {e}")
            return SFTP_FAILURE
    
    def stat(self, path):
        """Get file attributes"""
        logger.info(f"STAT: {path}")
        try:
            real_path = self._realpath(path)
            logger.info(f"STAT real path: {real_path}")
            
            if not os.path.exists(real_path):
                logger.error(f"STAT file not found: {real_path}")
                return SFTP_NO_SUCH_FILE
            
            stat_result = os.stat(real_path)
            attr = SFTPAttributes.from_stat(stat_result)
            logger.info(f"STAT success: size={attr.st_size}, mode={attr.st_mode}")
            return attr
            
        except Exception as e:
            logger.error(f"Stat error: {e}")
            return SFTP_NO_SUCH_FILE
    
    def lstat(self, path):
        """Get file attributes (don't follow symlinks)"""
        return self.stat(path)
    
    def remove(self, path):
        """Delete a file"""
        logger.info(f"REMOVE: {path}")
        try:
            real_path = self._realpath(path)
            os.remove(real_path)
            return SFTP_OK
        except FileNotFoundError:
            return SFTP_NO_SUCH_FILE
        except Exception as e:
            logger.error(f"Remove error: {e}")
            return SFTP_FAILURE
    
    def rename(self, oldpath, newpath):
        """Rename a file"""
        logger.info(f"RENAME: {oldpath} -> {newpath}")
        try:
            old_real = self._realpath(oldpath)
            new_real = self._realpath(newpath)
            os.rename(old_real, new_real)
            return SFTP_OK
        except Exception as e:
            logger.error(f"Rename error: {e}")
            return SFTP_FAILURE
    
    def mkdir(self, path, attr):
        """Create a directory"""
        logger.info(f"MKDIR: {path}")
        try:
            real_path = self._realpath(path)
            os.mkdir(real_path)
            return SFTP_OK
        except Exception as e:
            logger.error(f"Mkdir error: {e}")
            return SFTP_FAILURE
    
    def rmdir(self, path):
        """Remove a directory"""
        logger.info(f"RMDIR: {path}")
        try:
            real_path = self._realpath(path)
            os.rmdir(real_path)
            return SFTP_OK
        except Exception as e:
            logger.error(f"Rmdir error: {e}")
            return SFTP_FAILURE
    
    def chattr(self, path, attr):
        """Change file attributes"""
        return SFTP_OK  # Not implemented, but return OK
    
    def readlink(self, path):
        """Read a symbolic link"""
        return SFTP_FAILURE  # Not implemented
    
    def symlink(self, target_path, path):
        """Create a symbolic link"""
        return SFTP_FAILURE  # Not implemented

class TestSSHServer(ServerInterface):
    """SSH server implementation"""
    def __init__(self, username='test', password='test123'):
        self.username = username
        self.password = password
        self.event = threading.Event()
    
    def check_channel_request(self, kind, chanid):
        if kind == 'session':
            return paramiko.OPEN_SUCCEEDED
        return paramiko.OPEN_FAILED_ADMINISTRATIVELY_PROHIBITED
    
    def check_auth_password(self, username, password):
        logger.info(f"Auth attempt: username={username}")
        if username == self.username and password == self.password:
            logger.info("Auth successful")
            return paramiko.AUTH_SUCCESSFUL
        logger.warning("Auth failed")
        return paramiko.AUTH_FAILED
    
    def get_allowed_auths(self, username):
        return 'password'
    
    def check_channel_shell_request(self, channel):
        return False
    
    def check_channel_pty_request(self, channel, term, width, height, pixelwidth, pixelheight, modes):
        return False

class SFTPTestServer:
    """Main SFTP test server"""
    def __init__(self, host='127.0.0.1', port=2250, username='test', password='test123', root=None):
        self.host = host
        self.port = port
        self.username = username
        self.password = password
        self.root = root or os.path.join(os.path.expanduser('~'), 'sftp_test')
        self.sock = None
        self.running = False
        self.threads = []
        
        # Create test directory
        os.makedirs(self.root, exist_ok=True)
        
        # Generate host key
        self.host_key = paramiko.RSAKey.generate(2048)
        
    def create_test_files(self):
        """Create some test files"""
        # Create test.txt
        test_file = os.path.join(self.root, "test.txt")
        with open(test_file, 'w') as f:
            f.write("Hello from Python Paramiko SFTP server!")
        
        # Create test directory
        test_dir = os.path.join(self.root, "test")
        os.makedirs(test_dir, exist_ok=True)
        
        # Create data.zip
        import zipfile
        test_zip = os.path.join(test_dir, "data.zip")
        with zipfile.ZipFile(test_zip, 'w') as zf:
            zf.writestr("content.txt", "Python Paramiko SFTP test content")
        
        # Create package.zip
        package_zip = os.path.join(self.root, "package.zip")
        with zipfile.ZipFile(package_zip, 'w') as zf:
            zf.writestr("readme.txt", "Test package content")
            zf.writestr("data.txt", "Sample data for testing")
        
        logger.info(f"Created test files in {self.root}")
    
    def handle_client(self, client_socket, addr):
        """Handle a client connection"""
        logger.info(f"Connection from {addr}")
        transport = None
        
        try:
            transport = paramiko.Transport(client_socket)
            transport.add_server_key(self.host_key)
            
            # Create custom SFTP server with root directory
            def sftp_factory(*args, **kwargs):
                kwargs['root'] = self.root
                return TestSFTPServer(*args, **kwargs)
            
            transport.set_subsystem_handler('sftp', paramiko.SFTPServer, sftp_factory)
            
            # Start SSH server
            server = TestSSHServer(self.username, self.password)
            transport.start_server(server=server)
            
            # Wait for client to disconnect
            while transport.is_active():
                time.sleep(0.5)
                
        except Exception as e:
            logger.error(f"Client handling error: {e}")
        finally:
            if transport:
                transport.close()
            client_socket.close()
            logger.info(f"Disconnected from {addr}")
    
    def start(self):
        """Start the SFTP server"""
        try:
            # Create socket
            self.sock = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
            self.sock.setsockopt(socket.SOL_SOCKET, socket.SO_REUSEADDR, 1)
            self.sock.bind((self.host, self.port))
            self.sock.listen(5)
            self.running = True
            
            # Create test files
            self.create_test_files()
            
            logger.info(f"SFTP server started on {self.host}:{self.port}")
            logger.info(f"Username: {self.username}, Password: {self.password}")
            logger.info(f"Root directory: {self.root}")
            
            # Accept connections
            while self.running:
                try:
                    self.sock.settimeout(1.0)  # Allow checking self.running
                    client, addr = self.sock.accept()
                    
                    # Handle client in a new thread
                    thread = threading.Thread(target=self.handle_client, args=(client, addr))
                    thread.daemon = True
                    thread.start()
                    self.threads.append(thread)
                    
                except socket.timeout:
                    continue
                except Exception as e:
                    if self.running:
                        logger.error(f"Accept error: {e}")
                    
        except Exception as e:
            logger.error(f"Server error: {e}")
            raise
        finally:
            self.stop()
    
    def stop(self):
        """Stop the SFTP server"""
        self.running = False
        if self.sock:
            self.sock.close()
        
        # Wait for threads to finish
        for thread in self.threads:
            thread.join(timeout=1.0)
        
        logger.info("SFTP server stopped")

def main():
    """Main entry point"""
    parser = argparse.ArgumentParser(description='SFTP Test Server')
    parser.add_argument('--host', default='127.0.0.1', help='Host to bind to')
    parser.add_argument('--port', type=int, default=2250, help='Port to listen on')
    parser.add_argument('--username', default='test', help='Username for authentication')
    parser.add_argument('--password', default='test123', help='Password for authentication')
    parser.add_argument('--root', help='Root directory for SFTP')
    parser.add_argument('--config', help='JSON config file')
    
    args = parser.parse_args()
    
    # Load config from file if specified
    if args.config and os.path.exists(args.config):
        with open(args.config, 'r') as f:
            config = json.load(f)
            args.host = config.get('host', args.host)
            args.port = config.get('port', args.port)
            args.username = config.get('username', args.username)
            args.password = config.get('password', args.password)
            args.root = config.get('root', args.root)
    
    # Create and start server
    server = SFTPTestServer(
        host=args.host,
        port=args.port,
        username=args.username,
        password=args.password,
        root=args.root
    )
    
    try:
        server.start()
    except KeyboardInterrupt:
        logger.info("Shutting down...")
        server.stop()

if __name__ == '__main__':
    main()