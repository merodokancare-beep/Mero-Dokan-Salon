import numpy as np
import scipy.integrate as integrate
import scipy.stats as stats

def test_formulas(mu, sigma, p):
    # Quantile
    zp = stats.norm.ppf(p)
    Cp = mu + zp * sigma
    
    # 1. Analytical Formula for E[S(X)]
    # E[S(X)] = sigma * (phi(zp) - zp * (1-p))
    phi_zp = stats.norm.pdf(zp)
    E_S_analytical = sigma * (phi_zp - zp * (1 - p))
    
    # 2. Numerical Integration
    # S(x) = max(0, x - Cp)
    integrand = lambda x: (x - Cp) * stats.norm.pdf(x, loc=mu, scale=sigma)
    E_S_numerical, _ = integrate.quad(integrand, Cp, np.inf)
    
    # 3. Monte Carlo Simulation
    np.random.seed(42)
    samples = np.random.normal(mu, sigma, 10000000)
    spills = np.maximum(0, samples - Cp)
    E_S_mc = np.mean(spills)
    
    print(f"p = {p}")
    print(f"  Analytical: {E_S_analytical:.8f}")
    print(f"  Numerical:  {E_S_numerical:.8f}")
    print(f"  Monte Carlo:{E_S_mc:.8f}")
    print(f"  Difference (Analyt - Num): {E_S_analytical - E_S_numerical:.8e}")

test_formulas(100, 15, 0.95)
test_formulas(100, 15, 0.99)
