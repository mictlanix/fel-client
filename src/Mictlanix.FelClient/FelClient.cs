//
// FelClient.cs
//
// Author:
//       Eddy Zavaleta <eddy@mictlanix.com>
//
// Copyright (c) 2015-2016 Eddy Zavaleta, Mictlanix, and contributors.
//
// Permission is hereby granted, free of charge, to any person obtaining a copy
// of this software and associated documentation files (the "Software"), to deal
// in the Software without restriction, including without limitation the rights
// to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
// copies of the Software, and to permit persons to whom the Software is
// furnished to do so, subject to the following conditions:
//
// The above copyright notice and this permission notice shall be included in
// all copies or substantial portions of the Software.
//
// THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
// IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
// FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
// AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
// LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
// OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN
// THE SOFTWARE.
using System;
using System.IO;
using System.Net;
using System.Net.Security;
using System.Security.Cryptography.X509Certificates;
using System.ServiceModel;
using System.Text;
using System.Security;
using System.Xml;
using Mictlanix.CFDv32;
using Mictlanix.Fel.Client.Internals;

namespace Mictlanix.Fel.Client {
	public class FelClient {
		public static string URL_PRODUCTION = @"http://timbrado.facturarenlinea.com/WSTFD.svc";
		static readonly BasicHttpBinding binding = new BasicHttpBinding {
			MaxBufferPoolSize = int.MaxValue,
			MaxReceivedMessageSize = int.MaxValue,
			ReaderQuotas = new XmlDictionaryReaderQuotas {
				MaxDepth = int.MaxValue,
				MaxStringContentLength = int.MaxValue,
				MaxArrayLength = int.MaxValue,
				MaxBytesPerRead = int.MaxValue,
				MaxNameTableCharCount = int.MaxValue,
			}
		};

		string url;
		EndpointAddress address;

		public FelClient (string username, string password) : this (username, password, URL_PRODUCTION)
		{
		}

		public FelClient (string username, string password, string url)
		{
			Username = username;
			Password = password;
			Url = url;

			ServicePointManager.ServerCertificateValidationCallback = 
				(object sp, X509Certificate c, X509Chain r, SslPolicyErrors e) => true;
		}

		public string Username { get; protected set; }
		public string Password { get; protected set; }
		public string Url {
			get { return url;}
			set {
				if (url == value)
					return;

				url = value;
				address = new EndpointAddress (url);
			}
		}
		public string PrivateKey { get; set; }
		public string PrivateKeyPassword { get; set; }

		public TimbreFiscalDigital Stamp (string id, Comprobante cfd)
		{
			return Stamp (id, cfd.ToXmlString ());
		}

		public TimbreFiscalDigital Stamp (string id, string xml)
		{
			using (var ws = new WSTFDClient (binding, address)) {
				var response = ws.TimbrarCFDI (Username, Password, xml, id);

				if (!response.OperacionExitosa) {
					throw new FelClientException (response.CodigoRespuesta,
						response.MensajeError,
						response.MensajeErrorDetallado);
				}

				return new TimbreFiscalDigital {
					UUID = response.Timbre.UUID,
					FechaTimbrado = response.Timbre.FechaTimbrado,
					selloCFD = response.Timbre.SelloCFD,
					noCertificadoSAT = response.Timbre.NumeroCertificadoSAT,
					selloSAT = response.Timbre.SelloSAT
				};
			}
		}

		public bool Cancel (string issuer, string uuid)
		{
			using (var ws = new WSTFDClient (binding, address)) {
				var response = ws.CancelarCFDI (Username, Password, issuer, new string[] { uuid }, PrivateKey, PrivateKeyPassword);

				if (!response.OperacionExitosa) {
					var code = string.Empty;
					var details = response.MensajeErrorDetallado;

					if (response.DetallesCancelacion != null && response.DetallesCancelacion.Length > 0) {
						code = response.DetallesCancelacion [0].CodigoResultado;

						if (!string.IsNullOrWhiteSpace (details)) {
							details += "\n";
						}

						details = response.DetallesCancelacion [0].MensajeResultado;
					}

					throw new FelClientException (code, response.MensajeError, details);
				}

				return true;
			}
		}

		public TimbreFiscalDigital GetStamp (string issuer, string uuid)
		{
			using (var ws = new WSTFDClient (binding, address)) {
				var response = ws.ConsultarComplementoTimbre (Username, Password, uuid);

				if (!response.OperacionExitosa) {
					throw new FelClientException (response.CodigoRespuesta,
						response.MensajeError,
						response.MensajeErrorDetallado);
				}

				return new TimbreFiscalDigital {
					UUID = response.Timbre.UUID,
					FechaTimbrado = response.Timbre.FechaTimbrado,
					selloCFD = response.Timbre.SelloCFD,
					noCertificadoSAT = response.Timbre.NumeroCertificadoSAT,
					selloSAT = response.Timbre.SelloSAT
				};
			}
		}
	}
}

