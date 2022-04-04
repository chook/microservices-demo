// Copyright 2018 Google LLC
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      http://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

package main

import (
	"context"
	"go.opentelemetry.io/otel/trace"
	"math/rand"
	"net/http"
	"time"

	"github.com/google/uuid"
	"github.com/sirupsen/logrus"
	//"github.com/uptrace/opentelemetry-go-extra/otellogrus"
	// "go.opentelemetry.io/otel"
	// "go.opentelemetry.io/otel/trace"
	// "go.opentelemetry.io/otel/attribute"
)

type ctxKeyLog struct{}
type ctxKeyRequestID struct{}

type logHandler struct {
	log  *logrus.Logger
	next http.Handler
}

type responseRecorder struct {
	b      int
	status int
	w      http.ResponseWriter
}

func (r *responseRecorder) Header() http.Header { return r.w.Header() }

func (r *responseRecorder) Write(p []byte) (int, error) {
	if r.status == 0 {
		r.status = http.StatusOK
	}
	n, err := r.w.Write(p)
	r.b += n
	return n, err
}

func (r *responseRecorder) WriteHeader(statusCode int) {
	r.status = statusCode
	r.w.WriteHeader(statusCode)
}

func (lh *logHandler) ServeHTTP(w http.ResponseWriter, r *http.Request) {
	ctx := r.Context()
	requestID, _ := uuid.NewRandom()
	ctx = context.WithValue(ctx, ctxKeyRequestID{}, requestID.String())

	spanContext := trace.SpanContextFromContext(r.Context())
	start := time.Now()
	rr := &responseRecorder{w: w}
	log := lh.log.WithFields(logrus.Fields{
		"http_req_path":   r.URL.Path,
		"http_req_method": r.Method,
		"http_req_id":     requestID.String(),
		"http_req_ip":     readUserIP(r),
		"trace_id": 	   spanContext.TraceID(),
		"span_id":  	   spanContext.SpanID()})

	if v, ok := r.Context().Value(ctxKeySessionID{}).(string); ok {
		log = log.WithField("session", v)
	}

	log.Debugf("request %s %s started. requestID: %s", r.Method, r.URL.Path, requestID.String())
	defer func() {
		log = log.WithFields(logrus.Fields{
			"http_resp_took_ms": int64(time.Since(start) / time.Millisecond),
			"http_resp_status":  rr.status,
			"http_resp_bytes":   rr.b})

		if rr.status < 400 {
			log.Debugf("request %s %s completed with status: %d. requestID: %s", r.Method, r.URL.Path, rr.status, requestID.String())
		} else {
			log.Errorf("request %s %s failed with status: %d. requestID: %s", r.Method, r.URL.Path, rr.status, requestID.String())
		}
	}()

	ctx = context.WithValue(ctx, ctxKeyLog{}, log)
	r = r.WithContext(ctx)

	lh.next.ServeHTTP(rr, r)
}

func readUserIP(r *http.Request) string {
	ips := []string{"192.63.196.161", "172.217.18.110", "52.220.125.74", "151.101.194.28", "13.114.171.8", "34.206.39.153", "23.210.254.113",
		"151.101.129.111", "142.250.185.67", "151.101.64.144", "158.46.145.28"}

	return ips[rand.Intn(len(ips))]
}

func ensureSessionID(next http.Handler) http.HandlerFunc {
	return func(w http.ResponseWriter, r *http.Request) {
		var sessionID string
		c, err := r.Cookie(cookieSessionID)
		if err == http.ErrNoCookie {
			u, _ := uuid.NewRandom()
			sessionID = u.String()
			http.SetCookie(w, &http.Cookie{
				Name:   cookieSessionID,
				Value:  sessionID,
				MaxAge: cookieMaxAge,
			})
		} else if err != nil {
			return
		} else {
			sessionID = c.Value
		}
		ctx := context.WithValue(r.Context(), ctxKeySessionID{}, sessionID)
		r = r.WithContext(ctx)
		next.ServeHTTP(w, r)
	}
}
