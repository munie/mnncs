﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Net;

namespace mnn.net {
    public enum SockRequestContentMode {
        none = 0x2421,  // $! => none/unknown
        binary = 0x2422,// $" => default
        text = 0x2323,  // ## => text/plain
        url = 0x2324,   // #$ => text/url
    }

    [Serializable]
    public class SockRequest {
        private static readonly int CONTENT_MODE_BYTES = 2;
        private static readonly int BINARY_LENGTH_BYTES = 2;
        private static readonly int TEXT_LENGTH_BYTES = 4;

        public IPEndPoint lep { get; private set; }
        public IPEndPoint rep { get; private set; }
        public /*short*/ SockRequestContentMode content_mode { get; private set; }
        public /*short*/ int length { get; private set; }
        public byte[] data { get; private set; }

        public SockRequest(IPEndPoint _lep, IPEndPoint _rep, byte[] raw)
        {
            lep = _lep;
            rep = _rep;
            CheckContentMode(raw);
            CheckLengthAndData(raw);
        }

        public void SetData(byte[] content)
        {
            if (content == null) return;

            length = content.Length;
            data = content;
        }

        private void CheckContentMode(byte[] raw)
        {
            if (raw.Length < 2)
                this.content_mode = SockRequestContentMode.none;
            else {
                int tmp = raw[0] + (raw[1] << 8);

                if (!Enum.IsDefined(typeof(SockRequestContentMode), tmp))
                    tmp = (int)SockRequestContentMode.none;

                this.content_mode = (SockRequestContentMode)tmp;
            }
        }

        private void CheckLengthAndData(byte[] raw)
        {
            switch (content_mode) {
                case SockRequestContentMode.binary:
                    if (raw.Length < 4) {
                        this.length = 0;
                        this.data = new byte[0];
                    } else {
                        this.length = System.Math.Min(raw[2] + (raw[3] << 8), raw.Length);
                        this.data = raw.Take(this.length).Skip(CONTENT_MODE_BYTES + BINARY_LENGTH_BYTES).ToArray();
                    }
                    break;

                case SockRequestContentMode.text:
                case SockRequestContentMode.url:
                    byte[] tmp = raw.Skip(CONTENT_MODE_BYTES).Take(TEXT_LENGTH_BYTES).ToArray();
                    this.length = int.Parse(Encoding.ASCII.GetString(tmp)); // ascii is better
                    this.data = raw.Take(this.length).Skip(CONTENT_MODE_BYTES + TEXT_LENGTH_BYTES).ToArray();
                    break;

                case SockRequestContentMode.none:
                default:
                    this.length = raw.Length;
                    this.data = raw;
                    break;
            }
        }

        public static void InsertHeader(SockRequestContentMode mode, ref byte[] buffer)
        {
            if (buffer == null || !Enum.IsDefined(typeof(SockRequestContentMode), mode))
                return;

            int tmp = (int)mode;
            int len = 0;

            switch (mode) {
                case SockRequestContentMode.binary:
                    len = CONTENT_MODE_BYTES + BINARY_LENGTH_BYTES + buffer.Length;
                    buffer = new byte[] { (byte)(tmp & 0xff), (byte)(tmp >> 8 & 0xff),
                        (byte)(len & 0xff), (byte)(len >> 8 & 0xff) }
                        .Concat(buffer).ToArray();
                    break;

                case SockRequestContentMode.text:
                case SockRequestContentMode.url:
                    len = CONTENT_MODE_BYTES + TEXT_LENGTH_BYTES + buffer.Length;
                    len += 10000;
                    byte[] len_byte = Encoding.ASCII.GetBytes(len.ToString());
                    len_byte = len_byte.Skip(len_byte.Length - 4).ToArray();
                    buffer = new byte[] { (byte)(tmp & 0xff), (byte)(tmp >> 8 & 0xff),
                        len_byte[0], len_byte[1], len_byte[2], len_byte[3] }
                        .Concat(buffer).ToArray();
                    break;

                case SockRequestContentMode.none:
                default:
                    break;
            }
        }
    }
}
