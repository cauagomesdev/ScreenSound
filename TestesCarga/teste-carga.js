import http from 'k6/http';
import exec from 'k6/execution';
import { check, sleep } from 'k6';

// =====================================================================
//  Teste de carga / estresse da ScreenSound.API
//
//  Cada usuário virtual (VU) simula uma pessoa navegando: consulta TODOS
//  os endpoints GET da API e depois "pensa" por 1 segundo antes de repetir.
//
//  Modos (escolhidos com -e TIPO=...):
//    carga    (padrão)  sobe até VUS usuários em RAMPA segundos e mantém por DURACAO
//                       k6 run TestesCarga/teste-carga.js
//                       k6 run -e VUS=200 -e DURACAO=1m TestesCarga/teste-carga.js
//    estresse           sobe os usuários em degraus até o sistema quebrar;
//                       o teste é interrompido quando os limites são estourados
//                       k6 run -e TIPO=estresse TestesCarga/teste-carga.js
//
//  Outras variáveis: BASE_URL, EMAIL, SENHA, PAUSA (segundos entre iterações),
//  RAMPA (segundos de subida no modo carga).
// =====================================================================

const BASE_URL = __ENV.BASE_URL || 'http://localhost:5241';
const EMAIL = __ENV.EMAIL || 'k6@screensound.com';
const SENHA = __ENV.SENHA || 'K6@teste123';
const PAUSA = Number(__ENV.PAUSA || 1);
const RAMPA = Number(__ENV.RAMPA || 10);
const TIPO = (__ENV.TIPO || 'carga').toLowerCase();

// Critério de "o sistema aguenta": menos de 1% de erros e 95% das respostas em até 1s.
// No modo carga os critérios valem só para o patamar (depois da rampa), quando todos
// os usuários já estão conectados; no estresse valem para o teste todo e o interrompem.
const LIMITES =
    TIPO === 'carga'
        ? {
              'http_req_failed{fase:patamar}': ['rate<0.01'],
              'http_req_duration{fase:patamar}': ['p(95)<1000'],
              'checks{fase:patamar}': ['rate>0.99'],
          }
        : {
              http_req_failed: [{ threshold: 'rate<0.01', abortOnFail: true, delayAbortEval: '10s' }],
              http_req_duration: [{ threshold: 'p(95)<1000', abortOnFail: true, delayAbortEval: '10s' }],
              checks: ['rate>0.99'],
          };

const CENARIOS = {
    carga: {
        executor: 'ramping-vus',
        startVUs: 0,
        stages: [
            { duration: `${RAMPA}s`, target: Number(__ENV.VUS || 10) }, // usuários chegando aos poucos
            { duration: __ENV.DURACAO || '30s', target: Number(__ENV.VUS || 10) }, // patamar
        ],
        gracefulRampDown: '5s',
    },
    estresse: {
        executor: 'ramping-vus',
        startVUs: 0,
        // Sobe em degraus e segura cada patamar para medir o comportamento nele.
        stages: [
            { duration: '15s', target: 50 },
            { duration: '30s', target: 50 },
            { duration: '15s', target: 100 },
            { duration: '30s', target: 100 },
            { duration: '15s', target: 200 },
            { duration: '30s', target: 200 },
            { duration: '15s', target: 400 },
            { duration: '30s', target: 400 },
            { duration: '15s', target: 800 },
            { duration: '30s', target: 800 },
            { duration: '15s', target: 1600 },
            { duration: '30s', target: 1600 },
            { duration: '15s', target: 0 },
        ],
        gracefulRampDown: '10s',
    },
};

if (!CENARIOS[TIPO]) {
    throw new Error(`TIPO inválido: "${TIPO}". Use "carga" ou "estresse".`);
}

export const options = {
    scenarios: { [TIPO]: CENARIOS[TIPO] },
    thresholds: LIMITES,
};

const JSON_HEADERS = { 'Content-Type': 'application/json' };

// Roda UMA vez antes do teste: garante usuário logado e dados para as consultas.
export function setup() {
    const credenciais = JSON.stringify({ email: EMAIL, password: SENHA });

    // Cadastra o usuário de teste; se ele já existir a API responde 400, sem problema.
    http.post(`${BASE_URL}/auth/register`, credenciais, {
        headers: JSON_HEADERS,
        responseCallback: http.expectedStatuses(200, 400),
    });

    const login = http.post(`${BASE_URL}/auth/login`, credenciais, { headers: JSON_HEADERS });
    if (login.status !== 200) {
        throw new Error(`Falha no login (status ${login.status}). A API está rodando em ${BASE_URL}?`);
    }
    const token = login.json('accessToken');
    const auth = { headers: { ...JSON_HEADERS, Authorization: `Bearer ${token}` } };

    // As consultas por nome/id precisam de registros reais: pega o primeiro de cada lista.
    const artistas = http.get(`${BASE_URL}/artistas`, auth).json();
    const musicas = http.get(`${BASE_URL}/musicas`, auth).json();
    let generos = http.get(`${BASE_URL}/generos`, auth).json();

    if (generos.length === 0) {
        http.post(`${BASE_URL}/generos`, JSON.stringify({ nome: 'Rock', descricao: 'Criado pelo teste k6' }), auth);
        generos = http.get(`${BASE_URL}/generos`, auth).json();
    }
    if (artistas.length === 0 || musicas.length === 0) {
        throw new Error('Cadastre ao menos um artista e uma música antes de rodar o teste.');
    }

    return {
        token,
        artista: artistas[0],
        musica: musicas[0],
        genero: generos[0],
    };
}

// "rampa" enquanto os usuários ainda estão chegando, "patamar" depois disso.
function fase() {
    return exec.instance.currentTestRunDuration >= RAMPA * 1000 ? 'patamar' : 'rampa';
}

// Faz um GET autenticado, nomeando a requisição (para métricas por endpoint) e checando o 200.
function consultar(dados, rota, nome) {
    const tags = { name: nome, fase: fase() };
    const res = http.get(`${BASE_URL}${rota}`, {
        headers: { Authorization: `Bearer ${dados.token}` },
        tags,
    });
    check(res, { [`${nome} -> status 200`]: (r) => r.status === 200 }, tags);
}

// Executada repetidamente por cada usuário virtual.
export default function (dados) {
    const nomeArtista = encodeURIComponent(dados.artista.nome);
    const nomeMusica = encodeURIComponent(dados.musica.nome);
    const nomeGenero = encodeURIComponent(dados.genero.nome);

    consultar(dados, '/artistas', 'GET /artistas');
    consultar(dados, `/artistas/${nomeArtista}`, 'GET /artistas/{nome}');
    consultar(dados, `/artistas/${dados.artista.id}/avaliacao`, 'GET /artistas/{id}/avaliacao');
    consultar(dados, '/musicas', 'GET /musicas');
    consultar(dados, `/musicas/${nomeMusica}`, 'GET /musicas/{nome}');
    consultar(dados, '/generos', 'GET /generos');
    consultar(dados, `/generos/${nomeGenero}`, 'GET /generos/{nome}');
    consultar(dados, '/auth/manage/info', 'GET /auth/manage/info');

    sleep(PAUSA); // tempo que uma pessoa real leva entre uma navegação e outra
}
