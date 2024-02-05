const opentelemetry = require("@opentelemetry/sdk-node");
const { getNodeAutoInstrumentations } = require("@opentelemetry/auto-instrumentations-node");
const { OTLPTraceExporter } =  require('@opentelemetry/exporter-trace-otlp-grpc');
const {CoralogixTransactionSampler} = require('@coralogix/opentelemetry');

const sdk = new opentelemetry.NodeSDK({
  traceExporter: new OTLPTraceExporter(),
  instrumentations: [getNodeAutoInstrumentations()],
  sampler: new CoralogixTransactionSampler()
});

sdk.start()
