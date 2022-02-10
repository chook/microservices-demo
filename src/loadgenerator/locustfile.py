#!/usr/bin/python
#
# Copyright 2018 Google LLC
#
# Licensed under the Apache License, Version 2.0 (the "License");
# you may not use this file except in compliance with the License.
# You may obtain a copy of the License at
#
#      http://www.apache.org/licenses/LICENSE-2.0
#
# Unless required by applicable law or agreed to in writing, software
# distributed under the License is distributed on an "AS IS" BASIS,
# WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
# See the License for the specific language governing permissions and
# limitations under the License.

import random
from locust import HttpUser, TaskSet, between
from locust import HttpUser, TaskSet, task, between
from locust import LoadTestShape
from locust.user.wait_time import constant
from faker import Faker

from opentelemetry import trace
from opentelemetry.sdk.trace import TracerProvider
from opentelemetry.sdk.trace.export import (BatchSpanProcessor)
from opentelemetry.exporter.otlp.proto.grpc.trace_exporter import OTLPSpanExporter
from opentelemetry.instrumentation.requests import RequestsInstrumentor
from opentelemetry.instrumentation.urllib3 import URLLib3Instrumentor

tracer_provider = TracerProvider()
trace.set_tracer_provider(tracer_provider)
tracer_provider.add_span_processor(BatchSpanProcessor(OTLPSpanExporter()))

# Instrumenting manually to avoid error with locust gevent monkey
RequestsInstrumentor().instrument()
URLLib3Instrumentor().instrument()

fake = Faker()

product_weights = [0.1, 0.2, 0.2, 0.05, 0.03, 0.07, 0.05, 0.15, 0.08, 0.02]
products_start = [
    '0PUK6V6EV0',
    '1YMWWN1N4O',
    '2ZYFJ3GM2N',
    '66VCHSJNUP',
    '6E92ZMYYFZ',
    '9SIQT8TOJO',
    'L9ECAV7KIM',
    'LS4PSXUNUM',
    'OLJCESPC7Z',
    'OLJCESPRRR'] # CREATES ERROR

products = random.choices(products_start, product_weights, k=1000)
currencies_weights = [0.2, 0.6, 0.1, 0.1]
currencies = random.choices(['EUR', 'USD', 'JPY', 'CAD'], currencies_weights, k=1000)

class HackerBehavior(TaskSet):
    @task(10)
    def browseProduct(l):
        l.client.get("/product/OLJCESPRRR")

class UserBehavior(TaskSet):
    def on_start(l):
        l.for_checkout = {
            'email': fake.email,
            'street_address': fake.street_address(),
            'credit_card_number': fake.credit_card_number(random.choice(['visa', 'mastercard'])),
            'postcode': fake.postcode(),
            'city': fake.city(),
            'country_code': fake.country_code(),
            'credit_card_security_code': fake.credit_card_security_code()
        }
    
    @task(1)
    def index(l):
        l.client.get("/")

    @task(2)
    def setCurrency(l):
        l.client.post("/setCurrency", 
            {'currency_code': random.choice(currencies)})

    @task(10)
    def browseProduct(l):
        l.client.get("/product/" + random.choice(products))

    @task(3)
    def viewCart(l):
        l.client.get("/cart")

    @task(2)
    def addToCart(l):
        product = random.choice(products)
        l.client.get("/product/" + product)
        l.client.post("/cart", {
            'product_id': product,
            'quantity': random.choice([1,2,3,4,5,10])})

    @task(1)
    def checkout(l):
        l.client.post("/cart/checkout", {
            'email': l.for_checkout['email'],
            'street_address': l.for_checkout['street_address'],
            'zip_code': l.for_checkout['postcode'],
            'city': l.for_checkout['city'],
            'state': l.for_checkout['country_code'],
            'country': 'United States',
            'credit_card_number': l.for_checkout['credit_card_number'],
            'credit_card_expiration_month': '1',
            'credit_card_expiration_year': '2039',
            'credit_card_cvv': l.for_checkout['credit_card_security_code'],
        })

class WebsiteUser(HttpUser):
    tasks = [UserBehavior]
    wait_time = between(1, 10)
