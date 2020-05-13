﻿using System;
using System.IO;
using System.Net.Sockets;

namespace Core
{
    public class Client
    {
        private readonly Socket _clientSocket;
        private readonly Listener _serverListener;
        private readonly NetworkStream _clientStream;

        private byte[] _clientBuffer;
        private int _clientBufferLength;

        private short _packetLength;
        private byte _packetMainId;
        private byte _packetSubId;

        private byte _xorOffsetRecv = 200;
        private byte _xorOffsetSend = 200;

        private readonly byte[] _xorArray = { 0xC8, 0xB8, 0x1A, 0xB0, 0x42, 0xF7, 0x93, 0x5A, 0x0B, 0x14, 0x36, 0xCF, 0x54, 0x5F, 0x4E, 0x9F, 0xA6, 0xBF, 0x33, 0x12, 0x09, 0x95, 0x64, 0x1D, 0x9C, 0xB9, 0x2A, 0x0A, 0xF8, 0x62, 0x1B, 0x38, 0xE8, 0x76, 0x1F, 0x26, 0x9C, 0x2B, 0x9D, 0xEA, 0xDB, 0x0D, 0x25, 0x44, 0xD9, 0x45, 0x0C, 0xEE, 0xE6, 0xC2, 0xAA, 0x39, 0x26, 0x5C, 0xBC, 0xE9, 0x3E, 0x5A, 0x21, 0xB1, 0x4B, 0x02, 0xFE, 0x5A, 0x9A, 0xF0, 0x73, 0xB4, 0xAD, 0x7E, 0x93, 0xF4, 0x1A, 0x3E, 0x43, 0x5D, 0x12, 0xFE, 0x36, 0xE4, 0x99, 0x13, 0x11, 0xC9, 0x9F, 0x80, 0xDC, 0x1D, 0x0E, 0x10, 0x8B, 0x21, 0x5E, 0x6E, 0x75, 0x1D, 0x00, 0xEA, 0x32, 0xD7, 0x0F, 0x61, 0xD2, 0x4F, 0xB5, 0x6F, 0xBA, 0x40, 0x47, 0x38, 0xD2, 0xDB, 0x20, 0x35, 0x4C, 0x1E, 0xA6, 0xED, 0xE4, 0x61, 0xF5, 0x2C, 0xD7, 0xEC, 0x50, 0x74, 0x4C, 0x83, 0x35, 0x87, 0xA3, 0x97, 0xAA, 0xEC, 0x35, 0x70, 0xC7, 0xBB, 0x76, 0xA1, 0x8A, 0x86, 0xEF, 0x99, 0xBB, 0xE5, 0x5F, 0xC9, 0x85, 0xEC, 0x1F, 0xAD, 0xC5, 0x83, 0x08, 0xB1, 0xEB, 0xB5, 0x2E, 0x5A, 0xD5, 0x92, 0x27, 0xC2, 0x95, 0xB6, 0x21, 0xF6, 0x79, 0x7E, 0x94, 0xFC, 0xA4, 0x92, 0xD2, 0xE5, 0x3D, 0xCE, 0x96, 0x11, 0x59, 0x91, 0xCD, 0x33, 0x3E, 0xB2, 0x29, 0xDA, 0x9C, 0xB3, 0x28, 0xC7, 0xA2, 0xCA, 0x63, 0xF7, 0x8F, 0xEA, 0xC6, 0xCD, 0x43, 0x88, 0xF1, 0xD2, 0x44, 0x35, 0xFC, 0x38, 0x00, 0xFE, 0xEE, 0x82, 0xE5, 0x3F, 0x00, 0x54, 0x3A, 0xAA, 0xC7, 0x59, 0xE1, 0xFB, 0x1D, 0xE6, 0xEB, 0x3B, 0x06, 0xFB, 0x87, 0xDD, 0x6A, 0xF1, 0xE9, 0x71, 0x2C, 0x48, 0x92, 0xEA, 0x1A, 0x86, 0x49, 0x8A, 0xA3, 0x4E, 0x10, 0x6C, 0x84, 0x78, 0x0E, 0xBB, 0x8C, 0xA1, 0xE6, 0x13, 0x74, 0xCA };

        public Client(TcpClient tcpClient, Listener listenerParent)
        {
            _clientSocket = tcpClient.Client;
            _clientStream = tcpClient.GetStream();

            _serverListener = listenerParent;

            _clientBuffer = new byte[4];
            _clientBufferLength = _clientBuffer.Length;

            _clientStream.BeginRead(_clientBuffer, 0, _clientBufferLength, OnHeader, null);
        }

        private void OnHeader(IAsyncResult asyncResult)
        {
            if (_clientSocket.Poll(1, SelectMode.SelectRead) && _clientSocket.Available == 0)
            {
                Log.WriteInfo("Client connection lost or closed on. ( {0} )", _clientSocket.RemoteEndPoint);
                return;
            }

            _clientBufferLength -= _clientStream.EndRead(asyncResult);

            if (_clientBufferLength > 0)
            {
                _clientStream.BeginRead(_clientBuffer, _clientBuffer.Length - _clientBufferLength, _clientBufferLength,
                    OnHeader, _clientStream);
                return;
            }

            for (var i = 0; i < _clientBuffer.Length; i++)
                _clientBuffer[i] ^= _xorArray[_xorOffsetRecv++];

            _packetLength = BitConverter.ToInt16(_clientBuffer, 0);
            _packetMainId = _clientBuffer[2];
            _packetSubId = _clientBuffer[3];

            _clientBufferLength = _packetLength - 4;
            _clientBuffer = new byte[_clientBufferLength];

            _clientStream.BeginRead(_clientBuffer, 0, _clientBufferLength, OnData, null);
        }

        private void OnData(IAsyncResult asyncResult)
        {
            if (_clientSocket.Poll(1, SelectMode.SelectRead) && _clientSocket.Available == 0)
            {
                Log.WriteInfo("Client connection lost or closed on. ( {0} )", _clientSocket.RemoteEndPoint);
                return;
            }

            _clientBufferLength -= _clientStream.EndRead(asyncResult);

            if (_clientBufferLength > 0)
            {
                _clientStream.BeginRead(_clientBuffer, _clientBuffer.Length - _clientBufferLength, _clientBufferLength,
                    OnData, null);
                return;
            }

            for (var i = 0; i < _clientBuffer.Length; i++)
                _clientBuffer[i] ^= _xorArray[_xorOffsetRecv++];

            _serverListener.Handle(this, _packetLength, _packetMainId, _packetSubId, _clientBuffer);

            _clientBuffer = new byte[4];
            _clientBufferLength = _clientBuffer.Length;

            _clientStream.BeginRead(_clientBuffer, 0, _clientBufferLength, OnHeader, null);
        }

        public void SendPacket(Packet packetData)
        {
            if (_clientSocket.Poll(1, SelectMode.SelectRead) && _clientSocket.Available == 0)
            {
                Log.WriteInfo("Client connection lost or closed. ( {0} )", _clientSocket.RemoteEndPoint);
                return;
            }

            var bufferLength = Convert.ToInt16(packetData.GetBuffer().Length + 2);

            Log.WriteInfo("Packet sent. ( Length: {0} MainId: {1} SubId: {2} )", bufferLength, packetData.MainId, packetData.SubId);

            File.WriteAllBytes($".SC {packetData.MainId:D3}-{packetData.SubId:D3}-{bufferLength:D5}.dat", packetData.GetBuffer());

            var sendBuffer = new byte[bufferLength];

            Buffer.BlockCopy(BitConverter.GetBytes(bufferLength), 0, sendBuffer, 0, 2);
            Buffer.BlockCopy(packetData.GetBuffer(), 0, sendBuffer, 2, packetData.GetBuffer().Length);

            for (var i = 0; i < sendBuffer.Length; i++)
                sendBuffer[i] ^= _xorArray[_xorOffsetSend++];

            _clientStream.Write(sendBuffer, 0, sendBuffer.Length);
        }
    }
}