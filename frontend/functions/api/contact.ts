interface Env {
  GITHUB_TOKEN: string
}

export const onRequestPost: PagesFunction<Env> = async (context) => {
  const headers = {
    'Content-Type': 'application/json',
    'Access-Control-Allow-Origin': 'https://eyerest.net',
  }

  try {
    const { name, email, message } = await context.request.json()

    // Validate
    if (!name?.trim() || !email?.trim() || !message?.trim()) {
      return new Response(JSON.stringify({ error: 'All fields are required' }), { status: 400, headers })
    }
    if (!/^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email)) {
      return new Response(JSON.stringify({ error: 'Invalid email' }), { status: 400, headers })
    }

    // Create GitHub Issue
    const res = await fetch('https://api.github.com/repos/rockyway/eye-rest/issues', {
      method: 'POST',
      headers: {
        'Authorization': `token ${context.env.GITHUB_TOKEN}`,
        'Accept': 'application/vnd.github.v3+json',
        'User-Agent': 'EyeRest-Contact-Form',
        'Content-Type': 'application/json',
      },
      body: JSON.stringify({
        title: `[Contact] ${name}`,
        body: `**From:** ${name}\n**Email:** ${email}\n\n---\n\n${message}`,
        labels: ['contact'],
      }),
    })

    if (!res.ok) {
      return new Response(JSON.stringify({ error: 'Failed to submit' }), { status: 500, headers })
    }

    return new Response(JSON.stringify({ success: true }), { status: 200, headers })
  } catch {
    return new Response(JSON.stringify({ error: 'Server error' }), { status: 500, headers })
  }
}

// Handle CORS preflight
export const onRequestOptions: PagesFunction = async () => {
  return new Response(null, {
    headers: {
      'Access-Control-Allow-Origin': 'https://eyerest.net',
      'Access-Control-Allow-Methods': 'POST, OPTIONS',
      'Access-Control-Allow-Headers': 'Content-Type',
    },
  })
}
