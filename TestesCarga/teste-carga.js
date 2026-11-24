import http from 'k6/http';
import { check } from 'k6';

// Endereço da API e usuário do teste. Podem ser trocados na linha de comando, por exemplo:
//   k6 run -e BASE_URL=http://localhost:5241 -e EMAIL=eu@email.com -e SENHA=Minha@123 teste-carga.js
const BASE_URL = __ENV.BASE_URL || 'http://localhost:5241';
const EMAIL = __ENV.EMAIL || 'k6@screensound.com';
const SENHA = __ENV.SENHA || 'K6@teste123';

export const options = {
    vus: 10, // número de usuários virtuais simultâneos
    duration: '30s', // duração total do teste
};

// Roda UMA vez antes do teste: garante que o usuário existe e faz login.
// Necessário porque os endpoints /artistas exigem autenticação (RequireAuthorization).
export function setup() {
    const json = { headers: { 'Content-Type': 'application/json' } };
    const credenciais = JSON.stringify({ email: EMAIL, password: SENHA });

    // Cadastra o usuário; se ele já existir a API responde 400, o que não tem problema.
    http.post(`${BASE_URL}/auth/register`, credenciais, json);

    const login = http.post(`${BASE_URL}/auth/login`, credenciais, json);
    if (login.status !== 200) {
        throw new Error(`Falha no login (status ${login.status}). A API está rodando em ${BASE_URL}?`);
    }

    return { token: login.json('accessToken') };
}

// Executada repetidamente por cada usuário virtual durante o teste.
export default function (dados) {
    const res = http.get(`${BASE_URL}/artistas`, {
        headers: { Authorization: `Bearer ${dados.token}` },
    });

    check(res, {
        'status is 200': (r) => r.status === 200,
    });
}
